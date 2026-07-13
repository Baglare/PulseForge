using System;
using System.IO;
using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.Audio;

namespace PulseForge.Runtime.Unity.Persistence
{
    public sealed class SettingsRepository
    {
        private const string FileName = "settings.json";
        private readonly JsonFileStore store;

        public SettingsRepository(JsonFileStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public PulseForgeSettingsData Current { get; private set; }

        public PulseForgeSettingsData Load()
        {
            Current = store.Load(
                FileName,
                SaveDefaults.CreateSettings,
                SaveDataNormalizer.NormalizeSettings);
            return Current;
        }

        public bool Save(PulseForgeSettingsData settings)
        {
            Current = SaveDataNormalizer.NormalizeSettings(settings);
            return store.Save(FileName, Current, SaveDataNormalizer.NormalizeSettings);
        }

        public PulseForgeSettingsData ResetToDefaults()
        {
            Current = SaveDefaults.CreateSettings();
            Save(Current);
            return Current;
        }
    }

    public sealed class ProfileRepository
    {
        private const string FileName = "profile.json";
        private readonly JsonFileStore store;

        public ProfileRepository(JsonFileStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public PulseForgeProfileData Current { get; private set; }

        public PulseForgeProfileData Load()
        {
            Current = store.Load(
                FileName,
                SaveDefaults.CreateProfile,
                SaveDataNormalizer.NormalizeProfile);
            return Current;
        }

        public bool RecordCompletedSession(ScoreSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            Current = SaveDataNormalizer.NormalizeProfile(Current);
            Current.completedSessions++;
            Current.totalPerfect += snapshot.PerfectCount;
            Current.totalGood += snapshot.GoodCount;
            Current.totalMiss += snapshot.MissCount;
            Current.highestScore = Math.Max(Current.highestScore, snapshot.TotalScore);
            Current.highestCombo = Math.Max(Current.highestCombo, snapshot.MaxCombo);
            Current.lastPlayedAtUtc = SaveDefaults.UtcNow();
            return store.Save(FileName, Current, SaveDataNormalizer.NormalizeProfile);
        }

        public PulseForgeProfileData ResetToDefaults()
        {
            Current = SaveDefaults.CreateProfile();
            store.Save(FileName, Current, SaveDataNormalizer.NormalizeProfile);
            return Current;
        }
    }

    public sealed class TrackLibraryRepository
    {
        private const string FileName = "library.json";
        private readonly JsonFileStore store;

        public TrackLibraryRepository(JsonFileStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public SavedTrackLibraryData Current { get; private set; }

        public SavedTrackLibraryData Load()
        {
            Current = store.Load(
                FileName,
                SaveDefaults.CreateLibrary,
                SaveDataNormalizer.NormalizeLibrary);
            return Current;
        }

        internal bool AddOrUpdateTrackPreset(
            SavedTrackMetadata metadata,
            RuntimeAudioPipelineSettings settings,
            int eventCount,
            out SavedTrackPresetReference reference)
        {
            Current = SaveDataNormalizer.NormalizeLibrary(Current);
            string now = SaveDefaults.UtcNow();
            SavedTrackData track = FindTrack(metadata.TrackId);
            if (track == null)
            {
                track = new SavedTrackData
                {
                    trackId = metadata.TrackId,
                    displayName = metadata.DisplayName,
                    originalFilePath = metadata.OriginalFilePath,
                    sourceExtension = metadata.SourceExtension,
                    fileSizeBytes = metadata.FileSizeBytes,
                    durationSeconds = metadata.DurationSeconds,
                    contentHash = metadata.ContentHash,
                    fileMissing = false,
                    createdAtUtc = now,
                    lastUsedAtUtc = now,
                    presets = new System.Collections.Generic.List<SavedTrackPresetData>()
                };
                Current.tracks.Add(track);
            }
            else
            {
                track.displayName = metadata.DisplayName;
                track.originalFilePath = metadata.OriginalFilePath;
                track.sourceExtension = metadata.SourceExtension;
                track.fileSizeBytes = metadata.FileSizeBytes;
                track.durationSeconds = metadata.DurationSeconds;
                track.contentHash = metadata.ContentHash;
                track.fileMissing = false;
                track.lastUsedAtUtc = now;
            }

            SavedTrackPresetData preset = FindPresetBySettings(track, settings);
            if (preset == null)
            {
                preset = new SavedTrackPresetData
                {
                    presetId = Guid.NewGuid().ToString("N"),
                    detectionMode = settings.DetectionMode.ToString(),
                    difficulty = settings.Difficulty.ToString(),
                    combatStyle = settings.CombatStyle.ToString(),
                    eventCount = Math.Max(0, eventCount),
                    createdAtUtc = now,
                    updatedAtUtc = now,
                    lastPlayedAtUtc = string.Empty
                };
                track.presets.Add(preset);
            }
            else
            {
                preset.eventCount = Math.Max(0, eventCount);
                preset.updatedAtUtc = now;
            }

            reference = new SavedTrackPresetReference(track.trackId, preset.presetId);
            return Save();
        }

        public bool RecordPerformance(
            string trackId,
            string presetId,
            ScoreSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            SavedTrackData track = FindTrack(trackId);
            SavedTrackPresetData preset = FindPreset(track, presetId);
            if (track == null || preset == null)
            {
                return false;
            }

            bool isFirstPerformance = preset.playCount == 0;
            string now = SaveDefaults.UtcNow();
            preset.playCount++;
            preset.bestScore = Math.Max(preset.bestScore, snapshot.TotalScore);
            preset.maxCombo = Math.Max(preset.maxCombo, snapshot.MaxCombo);
            preset.bestPerfectCount = Math.Max(preset.bestPerfectCount, snapshot.PerfectCount);
            preset.bestGoodCount = Math.Max(preset.bestGoodCount, snapshot.GoodCount);
            preset.lowestMissCount = isFirstPerformance
                ? snapshot.MissCount
                : Math.Min(preset.lowestMissCount, snapshot.MissCount);
            preset.lastPlayedAtUtc = now;
            preset.updatedAtUtc = now;
            track.lastUsedAtUtc = now;
            return Save();
        }

        public bool RefreshMissingFileStates()
        {
            Current = SaveDataNormalizer.NormalizeLibrary(Current);
            for (int i = 0; i < Current.tracks.Count; i++)
            {
                SavedTrackData track = Current.tracks[i];
                bool missing = string.IsNullOrWhiteSpace(track.originalFilePath)
                    || !File.Exists(track.originalFilePath);
                track.fileMissing = missing;
            }

            return Save();
        }

        public bool RelinkTrack(string trackId, string newPath, long fileSizeBytes)
        {
            SavedTrackData track = FindTrack(trackId);
            if (track == null || string.IsNullOrWhiteSpace(newPath))
            {
                return false;
            }

            track.originalFilePath = newPath;
            track.sourceExtension = Path.GetExtension(newPath).TrimStart('.').ToUpperInvariant();
            track.fileSizeBytes = Math.Max(0L, fileSizeBytes);
            track.fileMissing = false;
            track.lastUsedAtUtc = SaveDefaults.UtcNow();
            return Save();
        }

        public bool MarkTrackUsed(string trackId)
        {
            SavedTrackData track = FindTrack(trackId);
            if (track == null)
            {
                return false;
            }

            track.lastUsedAtUtc = SaveDefaults.UtcNow();
            return Save();
        }

        public bool RemovePreset(string trackId, string presetId)
        {
            SavedTrackData track = FindTrack(trackId);
            if (track == null || track.presets == null)
            {
                return false;
            }

            for (int i = 0; i < track.presets.Count; i++)
            {
                if (string.Equals(track.presets[i].presetId, presetId, StringComparison.Ordinal))
                {
                    track.presets.RemoveAt(i);
                    return Save();
                }
            }

            return false;
        }

        public bool RemoveTrack(string trackId)
        {
            Current = SaveDataNormalizer.NormalizeLibrary(Current);
            for (int i = 0; i < Current.tracks.Count; i++)
            {
                if (string.Equals(Current.tracks[i].trackId, trackId, StringComparison.OrdinalIgnoreCase))
                {
                    Current.tracks.RemoveAt(i);
                    return Save();
                }
            }

            return false;
        }

        public SavedTrackData FindTrack(string trackId)
        {
            if (Current == null || Current.tracks == null || string.IsNullOrWhiteSpace(trackId))
            {
                return null;
            }

            for (int i = 0; i < Current.tracks.Count; i++)
            {
                SavedTrackData track = Current.tracks[i];
                if (track != null && string.Equals(
                    track.trackId,
                    trackId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return track;
                }
            }

            return null;
        }

        public SavedTrackPresetData FindPreset(SavedTrackData track, string presetId)
        {
            if (track == null || track.presets == null || string.IsNullOrWhiteSpace(presetId))
            {
                return null;
            }

            for (int i = 0; i < track.presets.Count; i++)
            {
                SavedTrackPresetData preset = track.presets[i];
                if (preset != null && string.Equals(preset.presetId, presetId, StringComparison.Ordinal))
                {
                    return preset;
                }
            }

            return null;
        }

        public SavedTrackLibraryData Clear()
        {
            Current = SaveDefaults.CreateLibrary();
            Save();
            return Current;
        }

        private SavedTrackPresetData FindPresetBySettings(
            SavedTrackData track,
            RuntimeAudioPipelineSettings settings)
        {
            string expected = SaveDataNormalizer.PresetKey(
                settings.DetectionMode.ToString(),
                settings.Difficulty.ToString(),
                settings.CombatStyle.ToString());
            for (int i = 0; i < track.presets.Count; i++)
            {
                SavedTrackPresetData preset = track.presets[i];
                string actual = SaveDataNormalizer.PresetKey(
                    preset.detectionMode,
                    preset.difficulty,
                    preset.combatStyle);
                if (string.Equals(expected, actual, StringComparison.Ordinal))
                {
                    return preset;
                }
            }

            return null;
        }

        private bool Save()
        {
            Current = SaveDataNormalizer.NormalizeLibrary(Current);
            return store.Save(FileName, Current, SaveDataNormalizer.NormalizeLibrary);
        }
    }
}
