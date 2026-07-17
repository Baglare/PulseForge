using System;
using System.Collections.Generic;
using PulseForge.AudioAnalysis;
using PulseForge.BeatMapGeneration;
using PulseForge.Domain.Rhythm;

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
        public string defaultCoverage;
        public string defaultGameMode;
        public string defaultTimingAssist;
        public string uiLanguage;
        public bool showUpcomingInputs;
        public bool beatPulseEnabled;
        public float forecastLeadMultiplier;
        public string readabilityMode;
        public float beatmapOffsetSeconds;
        public float inputTimingOffsetSeconds;
        public PulseForgeAudioSettingsData audio;
        public PulseForgeDisplaySettingsData display;
        public PulseForgeInputSettingsData input;
    }

    public enum RadialReadabilityMode
    {
        Standard,
        Assisted,
        HighClarity
    }

    public enum PulseForgeUILanguage
    {
        English,
        Turkish
    }

    [Serializable]
    public sealed class PulseForgeAudioSettingsData
    {
        public float masterVolume;
        public float musicVolume;
    }

    [Serializable]
    public sealed class PulseForgeDisplaySettingsData
    {
        public bool reservedFullscreen;
        public string displayMode;
        public int resolutionWidth;
        public int resolutionHeight;
        public int refreshRate;
        public bool vSync;
        public int frameRateLimit;
    }

    [Serializable]
    public sealed class PulseForgeInputSettingsData
    {
        public string reservedBindingProfile;
        public string inputBindingOverridesJson;
    }

    public enum PulseForgeDisplayMode
    {
        Windowed,
        Borderless,
        ExclusiveFullscreen
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
        public string originalFileName;
        public string originalFilePath;
        public string sourceExtension;
        public long fileSizeBytes;
        public double durationSeconds;
        public string contentHash;
        public bool fileMissing;
        public string cachedAudioRelativePath;
        public int cacheVersion;
        public int audioCacheVersion;
        public string createdAtUtc;
        public string updatedAtUtc;
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
        public string coverage;
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
        public string cachedBeatmapRelativePath;
        public int cacheVersion;
        public int beatMapCacheVersion;
        public int analyzerVersion;
        public int plannerVersion;
        public string beatMapFingerprint;
        public int inputCost;
        public string plannerResult;
        public string cacheStatus;
        public List<SavedTrackPerformanceData> performances;
    }

    public enum SavedTrackCacheStatus
    {
        Ready,
        NeedsRebuild,
        Damaged
    }

    [Serializable]
    public sealed class SavedBeatMapCacheData
    {
        public int cacheVersion;
        public string trackId;
        public string presetId;
        public string createdAtUtc;
        public string updatedAtUtc;
        public List<SavedBeatEventCacheData> events;
    }

    [Serializable]
    public sealed class SavedBeatEventCacheData
    {
        public string eventId;
        public double targetTimeSeconds;
        public string action;
        public float intensity;
    }

    [Serializable]
    public sealed class RadialBeatMapCacheData
    {
        public int beatMapCacheVersion;
        public int analyzerVersion;
        public int plannerVersion;
        public string trackId;
        public string presetId;
        public string beatMapFingerprint;
        public string createdAtUtc;
        public string updatedAtUtc;
        public RadialBeatMapData radialBeatMap;
        public BeatGridData beatGrid;
        public AnalyzerQualityReport analyzerQuality;
        public PlannerQualityReport plannerQuality;
    }

    [Serializable]
    public sealed class SavedTrackPerformanceData
    {
        public string gameMode;
        public string timingAssist;
        public string scoreSchema;
        public string beatMapFingerprint;
        public int playCount;
        public int attemptCount;
        public int clearCount;
        public int bestScore;
        public int maxCombo;
        public int bestPerfectCount;
        public int bestGoodCount;
        public int lowestMissCount;
        public string lastOutcome;
        public string lastPlayedAtUtc;
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
            string originalFileName,
            string originalFilePath,
            string sourceExtension,
            long fileSizeBytes,
            double durationSeconds,
            string contentHash)
        {
            TrackId = trackId;
            DisplayName = displayName;
            OriginalFileName = originalFileName;
            OriginalFilePath = originalFilePath;
            SourceExtension = sourceExtension;
            FileSizeBytes = fileSizeBytes;
            DurationSeconds = durationSeconds;
            ContentHash = contentHash;
        }

        public string TrackId { get; }
        public string DisplayName { get; }
        public string OriginalFileName { get; }
        public string OriginalFilePath { get; }
        public string SourceExtension { get; }
        public long FileSizeBytes { get; }
        public double DurationSeconds { get; }
        public string ContentHash { get; }
    }
}
