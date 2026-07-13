using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.Audio;
using UnityEngine;

namespace PulseForge.Runtime.Unity.Persistence
{
    public sealed class PulseForgeSaveService
    {
        private readonly JsonFileStore store;
        private readonly SettingsRepository settingsRepository;
        private readonly ProfileRepository profileRepository;
        private readonly TrackLibraryRepository libraryRepository;
        private readonly LibraryCacheStore cacheStore;
        private bool initialized;

        public PulseForgeSaveService()
        {
            string root = Path.Combine(Application.persistentDataPath, "PulseForge");
            store = new JsonFileStore(root);
            settingsRepository = new SettingsRepository(store);
            profileRepository = new ProfileRepository(store);
            libraryRepository = new TrackLibraryRepository(store);
            cacheStore = new LibraryCacheStore(root);
        }

        public string RootDirectory => store.RootDirectory;
        public PulseForgeSettingsData Settings { get; private set; }
        public PulseForgeProfileData Profile { get; private set; }
        public SavedTrackLibraryData Library { get; private set; }

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            Settings = settingsRepository.Load();
            Profile = profileRepository.Load();
            Library = libraryRepository.Load();
            initialized = true;
        }

        public bool SaveSettings(
            bool enableMotion,
            RuntimeAudioPipelineSettings pipelineSettings,
            float beatmapOffsetSeconds,
            float inputTimingOffsetSeconds)
        {
            EnsureInitialized();
            PulseForgeSettingsData settings = SaveDefaults.CloneSettings(Settings);
            settings.enableMotion = enableMotion;
            settings.defaultDetection = pipelineSettings.DetectionMode.ToString();
            settings.defaultDifficulty = pipelineSettings.Difficulty.ToString();
            settings.defaultCombatStyle = pipelineSettings.CombatStyle.ToString();
            settings.beatmapOffsetSeconds = beatmapOffsetSeconds;
            settings.inputTimingOffsetSeconds = inputTimingOffsetSeconds;
            return SaveSettings(settings);
        }

        public bool SaveSettings(PulseForgeSettingsData settings)
        {
            EnsureInitialized();
            bool saved = settingsRepository.Save(SaveDefaults.CloneSettings(settings));
            Settings = settingsRepository.Current;
            return saved;
        }

        public PulseForgeSettingsData ResetSettings()
        {
            EnsureInitialized();
            Settings = settingsRepository.ResetToDefaults();
            return Settings;
        }

        public PulseForgeProfileData ResetProfile()
        {
            EnsureInitialized();
            Profile = profileRepository.ResetToDefaults();
            return Profile;
        }

        public SavedTrackLibraryData ClearSavedTrackLibrary()
        {
            EnsureInitialized();
            Library = libraryRepository.Clear(out bool saved);
            if (saved)
            {
                cacheStore.ClearCache();
            }

            return Library;
        }

        public bool TrySaveTrackSetup(
            string sourcePath,
            string displayName,
            double durationSeconds,
            RuntimeAudioPipelineSettings pipelineSettings,
            string convertedWavPath,
            IReadOnlyList<BeatEventData> beatEvents,
            out SavedTrackPresetReference reference)
        {
            EnsureInitialized();
            reference = default;
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                Debug.LogWarning("PulseForge library save skipped because the source file is missing: " + sourcePath);
                return false;
            }

            try
            {
                FileInfo info = new FileInfo(sourcePath);
                string hash = ComputeSha256(sourcePath);
                SavedTrackData existingTrack = libraryRepository.FindTrack(hash);
                SavedTrackPresetData existingPreset = libraryRepository.FindPresetBySettings(
                    existingTrack,
                    pipelineSettings);
                string presetId = existingPreset == null
                    ? Guid.NewGuid().ToString("N")
                    : existingPreset.presetId;
                string presetCreatedAtUtc = existingPreset == null
                    ? SaveDefaults.UtcNow()
                    : existingPreset.createdAtUtc;
                if (!cacheStore.TryWritePresetCache(
                    hash,
                    presetId,
                    convertedWavPath,
                    beatEvents,
                    presetCreatedAtUtc,
                    out string cachedAudioRelativePath,
                    out string cachedBeatmapRelativePath,
                    out string cacheError))
                {
                    Debug.LogError("PulseForge library metadata was not updated: " + cacheError);
                    return false;
                }

                SavedTrackMetadata metadata = new SavedTrackMetadata(
                    hash,
                    string.IsNullOrWhiteSpace(displayName)
                        ? Path.GetFileNameWithoutExtension(sourcePath)
                        : displayName.Trim(),
                    Path.GetFileName(sourcePath),
                    sourcePath,
                    info.Extension.TrimStart('.').ToUpperInvariant(),
                    info.Length,
                    Math.Max(0d, durationSeconds),
                    hash);
                bool saved = libraryRepository.AddOrUpdateTrackPreset(
                    metadata,
                    pipelineSettings,
                    beatEvents == null ? 0 : beatEvents.Count,
                    presetId,
                    cachedAudioRelativePath,
                    cachedBeatmapRelativePath,
                    SaveDefaults.LibraryCacheVersion,
                    out reference);
                if (!saved)
                {
                    if (existingTrack == null)
                    {
                        cacheStore.DeleteTrackCache(hash);
                    }
                    else if (existingPreset == null)
                    {
                        cacheStore.DeletePresetCache(hash, presetId);
                    }

                    reference = default;
                    Library = libraryRepository.Load();
                    return false;
                }

                Library = libraryRepository.Current;
                return saved;
            }
            catch (Exception exception)
            {
                Debug.LogError("PulseForge could not add the track to the library: " + exception.Message);
                return false;
            }
        }

        public bool TryGetPreset(
            string trackId,
            string presetId,
            out SavedTrackData track,
            out SavedTrackPresetData preset,
            out RuntimeAudioPipelineSettings settings)
        {
            EnsureInitialized();
            track = libraryRepository.FindTrack(trackId);
            preset = libraryRepository.FindPreset(track, presetId);
            if (track == null || preset == null)
            {
                settings = RuntimeAudioPipelineSettings.Default;
                return false;
            }

            return SaveDataNormalizer.TryGetPipelineSettings(
                preset.detectionMode,
                preset.difficulty,
                preset.combatStyle,
                out settings);
        }

        public bool TryGetCachedPreset(
            string trackId,
            string presetId,
            out SavedTrackCacheLoadData loadData,
            out string errorMessage)
        {
            EnsureInitialized();
            loadData = null;
            errorMessage = string.Empty;
            if (!TryGetPreset(
                trackId,
                presetId,
                out SavedTrackData track,
                out SavedTrackPresetData preset,
                out RuntimeAudioPipelineSettings settings))
            {
                errorMessage = "Saved track preset was not found.";
                return false;
            }

            SavedTrackCacheStatus status = cacheStore.GetCacheStatus(track, preset);
            preset.cacheStatus = status.ToString();
            if (status != SavedTrackCacheStatus.Ready
                || !cacheStore.TryLoadPresetCache(
                    track,
                    preset,
                    out string cachedAudioPath,
                    out IReadOnlyList<BeatEventData> beatEvents,
                    out errorMessage))
            {
                if (string.IsNullOrWhiteSpace(errorMessage))
                {
                    errorMessage = "Saved track cache is missing or damaged.";
                }

                return false;
            }

            loadData = new SavedTrackCacheLoadData(
                track,
                preset,
                settings,
                cachedAudioPath,
                beatEvents);
            return true;
        }

        public bool TryRelinkTrack(string trackId, string selectedPath, out string errorMessage)
        {
            EnsureInitialized();
            errorMessage = string.Empty;
            SavedTrackData track = libraryRepository.FindTrack(trackId);
            if (track == null)
            {
                errorMessage = "Saved track was not found.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(selectedPath) || !File.Exists(selectedPath))
            {
                errorMessage = "The selected audio file does not exist.";
                return false;
            }

            try
            {
                string hash = ComputeSha256(selectedPath);
                if (!string.Equals(hash, track.contentHash, StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = "The selected file does not match this saved track.";
                    return false;
                }

                FileInfo info = new FileInfo(selectedPath);
                bool saved = libraryRepository.RelinkTrack(trackId, selectedPath, info.Length);
                Library = libraryRepository.Current;
                return saved;
            }
            catch (Exception exception)
            {
                errorMessage = "The track could not be relinked: " + exception.Message;
                Debug.LogError("PulseForge relink failed: " + exception.Message);
                return false;
            }
        }

        public bool RemoveTrack(string trackId)
        {
            EnsureInitialized();
            bool saved = libraryRepository.RemoveTrack(trackId);
            Library = libraryRepository.Current;
            if (saved)
            {
                cacheStore.DeleteTrackCache(trackId);
            }

            return saved;
        }

        public void MarkTrackUsed(string trackId)
        {
            EnsureInitialized();
            libraryRepository.MarkTrackUsed(trackId);
            Library = libraryRepository.Current;
        }

        public bool RemovePreset(string trackId, string presetId)
        {
            EnsureInitialized();
            bool saved = libraryRepository.RemovePreset(trackId, presetId, out bool removedTrack);
            Library = libraryRepository.Current;
            if (saved)
            {
                if (removedTrack)
                {
                    cacheStore.DeleteTrackCache(trackId);
                }
                else
                {
                    cacheStore.DeletePresetCache(trackId, presetId);
                }
            }

            return saved;
        }

        public void RefreshLibraryFileStates()
        {
            EnsureInitialized();
            libraryRepository.RefreshMissingFileStates();
            RefreshCacheStatuses();
            Library = libraryRepository.Current;
        }

        public void MarkPresetCacheDamaged(string trackId, string presetId)
        {
            EnsureInitialized();
            SavedTrackData track = libraryRepository.FindTrack(trackId);
            SavedTrackPresetData preset = libraryRepository.FindPreset(track, presetId);
            if (preset == null)
            {
                return;
            }

            preset.cacheStatus = SavedTrackCacheStatus.Damaged.ToString();
            libraryRepository.SaveCacheStatuses();
            Library = libraryRepository.Current;
        }

        public void RecordCompletedSession(
            ScoreSnapshot snapshot,
            string trackId,
            string presetId)
        {
            EnsureInitialized();
            profileRepository.RecordCompletedSession(snapshot);
            Profile = profileRepository.Current;
            if (!string.IsNullOrWhiteSpace(trackId) && !string.IsNullOrWhiteSpace(presetId))
            {
                libraryRepository.RecordPerformance(trackId, presetId, snapshot);
                Library = libraryRepository.Current;
            }
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        private void RefreshCacheStatuses()
        {
            SavedTrackLibraryData library = libraryRepository.Current;
            if (library == null || library.tracks == null)
            {
                return;
            }

            for (int i = 0; i < library.tracks.Count; i++)
            {
                SavedTrackData track = library.tracks[i];
                if (track == null || track.presets == null)
                {
                    continue;
                }

                for (int presetIndex = 0; presetIndex < track.presets.Count; presetIndex++)
                {
                    SavedTrackPresetData preset = track.presets[presetIndex];
                    if (preset != null)
                    {
                        preset.cacheStatus = cacheStore.GetCacheStatus(track, preset).ToString();
                    }
                }
            }

            libraryRepository.SaveCacheStatuses();
        }

        private void EnsureInitialized()
        {
            if (!initialized)
            {
                Initialize();
            }
        }
    }

    public sealed class SavedTrackCacheLoadData
    {
        public SavedTrackCacheLoadData(
            SavedTrackData track,
            SavedTrackPresetData preset,
            RuntimeAudioPipelineSettings settings,
            string cachedAudioPath,
            IReadOnlyList<BeatEventData> beatEvents)
        {
            Track = track;
            Preset = preset;
            Settings = settings;
            CachedAudioPath = cachedAudioPath;
            BeatEvents = beatEvents;
        }

        public SavedTrackData Track { get; }
        public SavedTrackPresetData Preset { get; }
        public RuntimeAudioPipelineSettings Settings { get; }
        public string CachedAudioPath { get; }
        public IReadOnlyList<BeatEventData> BeatEvents { get; }
    }
}
