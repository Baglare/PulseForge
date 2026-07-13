using System;
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
        private bool initialized;

        public PulseForgeSaveService()
        {
            string root = Path.Combine(Application.persistentDataPath, "PulseForge");
            store = new JsonFileStore(root);
            settingsRepository = new SettingsRepository(store);
            profileRepository = new ProfileRepository(store);
            libraryRepository = new TrackLibraryRepository(store);
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
            Settings.enableMotion = enableMotion;
            Settings.defaultDetection = pipelineSettings.DetectionMode.ToString();
            Settings.defaultDifficulty = pipelineSettings.Difficulty.ToString();
            Settings.defaultCombatStyle = pipelineSettings.CombatStyle.ToString();
            Settings.beatmapOffsetSeconds = beatmapOffsetSeconds;
            Settings.inputTimingOffsetSeconds = inputTimingOffsetSeconds;
            bool saved = settingsRepository.Save(Settings);
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
            Library = libraryRepository.Clear();
            return Library;
        }

        public bool TrySaveTrackSetup(
            string sourcePath,
            string displayName,
            double durationSeconds,
            RuntimeAudioPipelineSettings pipelineSettings,
            int eventCount,
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
                SavedTrackMetadata metadata = new SavedTrackMetadata(
                    hash,
                    string.IsNullOrWhiteSpace(displayName)
                        ? Path.GetFileNameWithoutExtension(sourcePath)
                        : displayName.Trim(),
                    sourcePath,
                    info.Extension.TrimStart('.').ToUpperInvariant(),
                    info.Length,
                    Math.Max(0d, durationSeconds),
                    hash);
                bool saved = libraryRepository.AddOrUpdateTrackPreset(
                    metadata,
                    pipelineSettings,
                    eventCount,
                    out reference);
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
            bool saved = libraryRepository.RemovePreset(trackId, presetId);
            Library = libraryRepository.Current;
            return saved;
        }

        public void RefreshLibraryFileStates()
        {
            EnsureInitialized();
            libraryRepository.RefreshMissingFileStates();
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

        private void EnsureInitialized()
        {
            if (!initialized)
            {
                Initialize();
            }
        }
    }
}
