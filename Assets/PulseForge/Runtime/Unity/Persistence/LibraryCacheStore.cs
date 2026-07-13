using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PulseForge.Domain.Rhythm;
using UnityEngine;

namespace PulseForge.Runtime.Unity.Persistence
{
    public sealed class LibraryCacheStore
    {
        private const string CacheDirectoryName = "LibraryCache";
        private const string AudioFileName = "audio.wav";
        private readonly string saveRootDirectory;
        private readonly string cacheRootDirectory;

        public LibraryCacheStore(string saveRootDirectory)
        {
            this.saveRootDirectory = saveRootDirectory
                ?? throw new ArgumentNullException(nameof(saveRootDirectory));
            cacheRootDirectory = Path.Combine(saveRootDirectory, CacheDirectoryName);
        }

        public bool TryWritePresetCache(
            string trackId,
            string presetId,
            string convertedWavPath,
            IReadOnlyList<BeatEventData> beatEvents,
            string createdAtUtc,
            out string cachedAudioRelativePath,
            out string cachedBeatmapRelativePath,
            out string errorMessage)
        {
            cachedAudioRelativePath = string.Empty;
            cachedBeatmapRelativePath = string.Empty;
            errorMessage = string.Empty;
            if (!TryBuildPaths(
                trackId,
                presetId,
                out string trackDirectory,
                out string audioPath,
                out string beatmapPath,
                out errorMessage))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(convertedWavPath) || !File.Exists(convertedWavPath))
            {
                errorMessage = "Converted WAV is missing.";
                return false;
            }

            if (new FileInfo(convertedWavPath).Length <= 44L)
            {
                errorMessage = "Converted WAV is not a valid PCM WAV file.";
                return false;
            }

            if (beatEvents == null)
            {
                errorMessage = "Analyzed beat events are missing.";
                return false;
            }

            string audioTemporaryPath = audioPath + ".tmp";
            string beatmapTemporaryPath = beatmapPath + ".tmp";
            try
            {
                Directory.CreateDirectory(trackDirectory);
                Directory.CreateDirectory(Path.GetDirectoryName(beatmapPath));
                TryDeleteFile(audioTemporaryPath);
                TryDeleteFile(beatmapTemporaryPath);

                File.Copy(convertedWavPath, audioTemporaryPath, true);
                SavedBeatMapCacheData beatMap = CreateBeatMapCache(
                    trackId,
                    presetId,
                    beatEvents,
                    createdAtUtc);
                File.WriteAllText(
                    beatmapTemporaryPath,
                    JsonUtility.ToJson(beatMap, true),
                    new UTF8Encoding(false));

                CommitTemporaryFile(audioTemporaryPath, audioPath);
                CommitTemporaryFile(beatmapTemporaryPath, beatmapPath);
                cachedAudioRelativePath = ToRelativePath(audioPath);
                cachedBeatmapRelativePath = ToRelativePath(beatmapPath);
                return true;
            }
            catch (Exception exception)
            {
                errorMessage = "Saved track cache could not be written: " + exception.Message;
                Debug.LogError("PulseForge library cache write failed: " + exception.Message);
                TryDeleteFile(audioTemporaryPath);
                TryDeleteFile(beatmapTemporaryPath);
                return false;
            }
        }

        public SavedTrackCacheStatus GetCacheStatus(
            SavedTrackData track,
            SavedTrackPresetData preset)
        {
            if (track == null || preset == null
                || track.cacheVersion <= 0
                || preset.cacheVersion <= 0
                || string.IsNullOrWhiteSpace(track.cachedAudioRelativePath)
                || string.IsNullOrWhiteSpace(preset.cachedBeatmapRelativePath))
            {
                return SavedTrackCacheStatus.NeedsRebuild;
            }

            if (track.cacheVersion != SaveDefaults.LibraryCacheVersion
                || preset.cacheVersion != SaveDefaults.LibraryCacheVersion)
            {
                return SavedTrackCacheStatus.NeedsRebuild;
            }

            return TryLoadPresetCache(track, preset, out _, out _, out _)
                ? SavedTrackCacheStatus.Ready
                : SavedTrackCacheStatus.Damaged;
        }

        public bool TryLoadPresetCache(
            SavedTrackData track,
            SavedTrackPresetData preset,
            out string cachedAudioPath,
            out IReadOnlyList<BeatEventData> beatEvents,
            out string errorMessage)
        {
            cachedAudioPath = string.Empty;
            beatEvents = Array.Empty<BeatEventData>();
            errorMessage = string.Empty;
            if (track == null || preset == null)
            {
                errorMessage = "Saved track preset was not found.";
                return false;
            }

            if (!TryBuildPaths(
                track.trackId,
                preset.presetId,
                out _,
                out string expectedAudioPath,
                out string expectedBeatmapPath,
                out errorMessage))
            {
                return false;
            }

            string expectedAudioRelativePath = ToRelativePath(expectedAudioPath);
            string expectedBeatmapRelativePath = ToRelativePath(expectedBeatmapPath);
            if (!RelativePathEquals(track.cachedAudioRelativePath, expectedAudioRelativePath)
                || !RelativePathEquals(preset.cachedBeatmapRelativePath, expectedBeatmapRelativePath))
            {
                errorMessage = "Saved cache metadata contains an invalid path.";
                return false;
            }

            if (!File.Exists(expectedAudioPath) || new FileInfo(expectedAudioPath).Length <= 44L)
            {
                errorMessage = "Cached WAV is missing or damaged.";
                return false;
            }

            if (!TryReadBeatMap(
                expectedBeatmapPath,
                track.trackId,
                preset.presetId,
                out List<BeatEventData> loadedEvents,
                out errorMessage))
            {
                return false;
            }

            if (loadedEvents.Count != preset.eventCount)
            {
                errorMessage = "Cached beatmap event count does not match library metadata.";
                return false;
            }

            cachedAudioPath = expectedAudioPath;
            beatEvents = loadedEvents;
            return true;
        }

        public bool DeletePresetCache(string trackId, string presetId)
        {
            if (!TryBuildPaths(
                trackId,
                presetId,
                out _,
                out _,
                out string beatmapPath,
                out string errorMessage))
            {
                Debug.LogWarning("PulseForge cache cleanup skipped: " + errorMessage);
                return false;
            }

            return TryDeleteWithLog(beatmapPath, "preset beatmap cache");
        }

        public bool DeleteTrackCache(string trackId)
        {
            if (!IsSafeIdentifier(trackId))
            {
                Debug.LogWarning("PulseForge track cache cleanup skipped because the track id is invalid.");
                return false;
            }

            string trackDirectory = Path.Combine(cacheRootDirectory, trackId);
            return TryDeleteDirectoryWithLog(trackDirectory, "track cache");
        }

        public bool ClearCache()
        {
            return TryDeleteDirectoryWithLog(cacheRootDirectory, "library cache");
        }

        private static SavedBeatMapCacheData CreateBeatMapCache(
            string trackId,
            string presetId,
            IReadOnlyList<BeatEventData> beatEvents,
            string createdAtUtc)
        {
            string now = SaveDefaults.UtcNow();
            SavedBeatMapCacheData data = new SavedBeatMapCacheData
            {
                cacheVersion = SaveDefaults.LibraryCacheVersion,
                trackId = trackId,
                presetId = presetId,
                createdAtUtc = string.IsNullOrWhiteSpace(createdAtUtc) ? now : createdAtUtc,
                updatedAtUtc = now,
                events = new List<SavedBeatEventCacheData>(beatEvents.Count)
            };
            for (int i = 0; i < beatEvents.Count; i++)
            {
                BeatEventData beatEvent = beatEvents[i]
                    ?? throw new InvalidDataException("Beatmap contains a null event.");
                data.events.Add(new SavedBeatEventCacheData
                {
                    eventId = beatEvent.EventId,
                    targetTimeSeconds = beatEvent.TargetTimeSeconds,
                    action = beatEvent.Action.ToString(),
                    intensity = beatEvent.Intensity
                });
            }

            return data;
        }

        private static bool TryReadBeatMap(
            string beatmapPath,
            string expectedTrackId,
            string expectedPresetId,
            out List<BeatEventData> beatEvents,
            out string errorMessage)
        {
            beatEvents = new List<BeatEventData>();
            errorMessage = string.Empty;
            if (!File.Exists(beatmapPath))
            {
                errorMessage = "Cached beatmap is missing.";
                return false;
            }

            try
            {
                string json = File.ReadAllText(beatmapPath, Encoding.UTF8);
                SavedBeatMapCacheData data = JsonUtility.FromJson<SavedBeatMapCacheData>(json);
                if (data == null
                    || data.cacheVersion != SaveDefaults.LibraryCacheVersion
                    || !string.Equals(data.trackId, expectedTrackId, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(data.presetId, expectedPresetId, StringComparison.Ordinal)
                    || data.events == null)
                {
                    throw new InvalidDataException("Beatmap header is invalid.");
                }

                HashSet<string> eventIds = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < data.events.Count; i++)
                {
                    SavedBeatEventCacheData dto = data.events[i];
                    if (dto == null
                        || !Enum.TryParse(dto.action, true, out RhythmAction action)
                        || !Enum.IsDefined(typeof(RhythmAction), action)
                        || !eventIds.Add(dto.eventId ?? string.Empty))
                    {
                        throw new InvalidDataException("Beatmap event data is invalid.");
                    }

                    beatEvents.Add(new BeatEventData(
                        dto.eventId,
                        dto.targetTimeSeconds,
                        action,
                        dto.intensity));
                }

                return true;
            }
            catch (Exception exception)
            {
                beatEvents.Clear();
                errorMessage = "Cached beatmap is damaged: " + exception.Message;
                return false;
            }
        }

        private bool TryBuildPaths(
            string trackId,
            string presetId,
            out string trackDirectory,
            out string audioPath,
            out string beatmapPath,
            out string errorMessage)
        {
            trackDirectory = string.Empty;
            audioPath = string.Empty;
            beatmapPath = string.Empty;
            errorMessage = string.Empty;
            if (!IsSafeIdentifier(trackId) || !IsSafeIdentifier(presetId))
            {
                errorMessage = "Saved track cache identifier is invalid.";
                return false;
            }

            trackDirectory = Path.Combine(cacheRootDirectory, trackId);
            audioPath = Path.Combine(trackDirectory, AudioFileName);
            beatmapPath = Path.Combine(trackDirectory, "presets", presetId + ".json");
            return true;
        }

        private string ToRelativePath(string absolutePath)
        {
            string root = Path.GetFullPath(saveRootDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(absolutePath);
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Cache path escaped the PulseForge save directory.");
            }

            return fullPath.Substring(root.Length).Replace('\\', '/');
        }

        private static bool RelativePathEquals(string left, string right)
        {
            return string.Equals(
                (left ?? string.Empty).Replace('\\', '/').TrimStart('/'),
                (right ?? string.Empty).Replace('\\', '/').TrimStart('/'),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSafeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (!char.IsLetterOrDigit(character) && character != '-' && character != '_')
                {
                    return false;
                }
            }

            return true;
        }

        private static void CommitTemporaryFile(string temporaryPath, string destinationPath)
        {
            if (!File.Exists(destinationPath))
            {
                File.Move(temporaryPath, destinationPath);
                return;
            }

            string backupPath = destinationPath + ".bak";
            try
            {
                File.Replace(temporaryPath, destinationPath, backupPath, true);
                TryDeleteFile(backupPath);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(temporaryPath, destinationPath, true);
                File.Delete(temporaryPath);
            }
        }

        private static bool TryDeleteWithLog(string path, string description)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("PulseForge could not delete " + description + ": " + exception.Message);
                return false;
            }
        }

        private static bool TryDeleteDirectoryWithLog(string path, string description)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("PulseForge could not delete " + description + ": " + exception.Message);
                return false;
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // The primary cache operation reports any failure.
            }
        }
    }
}
