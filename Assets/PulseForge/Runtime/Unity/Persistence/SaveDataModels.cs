using System;
using System.Collections.Generic;

namespace PulseForge.Runtime.Unity.Persistence
{
    [Serializable]
    public sealed class PulseForgeSettingsData
    {
        public int schemaVersion;
        public bool enableMotion;
        public string defaultDetection;
        public string defaultDifficulty;
        public string defaultCombatStyle;
        public float beatmapOffsetSeconds;
        public float inputTimingOffsetSeconds;
        public PulseForgeAudioSettingsData audio;
        public PulseForgeDisplaySettingsData display;
        public PulseForgeInputSettingsData input;
    }

    [Serializable]
    public sealed class PulseForgeAudioSettingsData
    {
        public float masterVolume;
    }

    [Serializable]
    public sealed class PulseForgeDisplaySettingsData
    {
        public bool reservedFullscreen;
    }

    [Serializable]
    public sealed class PulseForgeInputSettingsData
    {
        public string reservedBindingProfile;
    }

    [Serializable]
    public sealed class PulseForgeProfileData
    {
        public int schemaVersion;
        public long completedSessions;
        public long totalPerfect;
        public long totalGood;
        public long totalMiss;
        public int highestScore;
        public int highestCombo;
        public string lastPlayedAtUtc;
    }

    [Serializable]
    public sealed class SavedTrackLibraryData
    {
        public int schemaVersion;
        public List<SavedTrackData> tracks;
    }

    [Serializable]
    public sealed class SavedTrackData
    {
        public string trackId;
        public string displayName;
        public string originalFilePath;
        public string sourceExtension;
        public long fileSizeBytes;
        public double durationSeconds;
        public string contentHash;
        public bool fileMissing;
        public string createdAtUtc;
        public string lastUsedAtUtc;
        public List<SavedTrackPresetData> presets;
    }

    [Serializable]
    public sealed class SavedTrackPresetData
    {
        public string presetId;
        public string detectionMode;
        public string difficulty;
        public string combatStyle;
        public int eventCount;
        public string createdAtUtc;
        public string updatedAtUtc;
        public string lastPlayedAtUtc;
        public int playCount;
        public int bestScore;
        public int maxCombo;
        public int bestPerfectCount;
        public int bestGoodCount;
        public int lowestMissCount;
    }

    public readonly struct SavedTrackPresetReference
    {
        public SavedTrackPresetReference(string trackId, string presetId)
        {
            TrackId = trackId ?? string.Empty;
            PresetId = presetId ?? string.Empty;
        }

        public string TrackId { get; }
        public string PresetId { get; }
        public bool IsValid => !string.IsNullOrEmpty(TrackId) && !string.IsNullOrEmpty(PresetId);
    }

    internal readonly struct SavedTrackMetadata
    {
        public SavedTrackMetadata(
            string trackId,
            string displayName,
            string originalFilePath,
            string sourceExtension,
            long fileSizeBytes,
            double durationSeconds,
            string contentHash)
        {
            TrackId = trackId;
            DisplayName = displayName;
            OriginalFilePath = originalFilePath;
            SourceExtension = sourceExtension;
            FileSizeBytes = fileSizeBytes;
            DurationSeconds = durationSeconds;
            ContentHash = contentHash;
        }

        public string TrackId { get; }
        public string DisplayName { get; }
        public string OriginalFilePath { get; }
        public string SourceExtension { get; }
        public long FileSizeBytes { get; }
        public double DurationSeconds { get; }
        public string ContentHash { get; }
    }
}
