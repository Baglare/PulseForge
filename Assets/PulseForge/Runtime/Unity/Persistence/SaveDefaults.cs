using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PulseForge.Runtime.Unity.Audio;
using UnityEngine;

namespace PulseForge.Runtime.Unity.Persistence
{
    public static class SaveDefaults
    {
        public const int SettingsSchemaVersion = 2;
        public const int ProfileSchemaVersion = 1;
        public const int LibrarySchemaVersion = 2;
        public const int LibraryCacheVersion = 1;

        public static PulseForgeSettingsData CreateSettings()
        {
            RuntimeAudioPipelineSettings pipeline = RuntimeAudioPipelineSettings.Default;
            Resolution currentResolution = Screen.currentResolution;
            int width = currentResolution.width > 0 ? currentResolution.width : 1920;
            int height = currentResolution.height > 0 ? currentResolution.height : 1080;
            int refreshRate = ResolveRefreshRate(currentResolution, 60);
            return new PulseForgeSettingsData
            {
                schemaVersion = SettingsSchemaVersion,
                enableMotion = true,
                defaultDetection = pipeline.DetectionMode.ToString(),
                defaultDifficulty = pipeline.Difficulty.ToString(),
                defaultCombatStyle = pipeline.CombatStyle.ToString(),
                beatmapOffsetSeconds = 0f,
                inputTimingOffsetSeconds = 0f,
                audio = new PulseForgeAudioSettingsData
                {
                    masterVolume = 1f,
                    musicVolume = 1f
                },
                display = new PulseForgeDisplaySettingsData
                {
                    displayMode = PulseForgeDisplayMode.Windowed.ToString(),
                    resolutionWidth = width,
                    resolutionHeight = height,
                    refreshRate = refreshRate,
                    vSync = true,
                    frameRateLimit = 120
                },
                input = new PulseForgeInputSettingsData
                {
                    reservedBindingProfile = string.Empty,
                    inputBindingOverridesJson = string.Empty
                }
            };
        }

        public static PulseForgeSettingsData CloneSettings(PulseForgeSettingsData source)
        {
            PulseForgeSettingsData clone = CreateSettings();
            if (source != null)
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), clone);
            }

            return SaveDataNormalizer.NormalizeSettings(clone);
        }

        public static PulseForgeProfileData CreateProfile()
        {
            return new PulseForgeProfileData
            {
                schemaVersion = ProfileSchemaVersion,
                lastPlayedAtUtc = string.Empty
            };
        }

        public static SavedTrackLibraryData CreateLibrary()
        {
            return new SavedTrackLibraryData
            {
                schemaVersion = LibrarySchemaVersion,
                tracks = new List<SavedTrackData>()
            };
        }

        public static string UtcNow()
        {
            return DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        }

        private static int ResolveRefreshRate(Resolution resolution, int fallback)
        {
            double value = resolution.refreshRateRatio.value;
            return value > 0d ? Math.Max(1, (int)Math.Round(value)) : fallback;
        }
    }

    public static class SaveDataNormalizer
    {
        private const float MaximumBeatmapOffsetSeconds = 0.5f;
        private const float MaximumInputOffsetSeconds = 0.5f;
        private const int MaximumBindingOverridesJsonLength = 64 * 1024;

        public static PulseForgeSettingsData NormalizeSettings(PulseForgeSettingsData data)
        {
            PulseForgeSettingsData defaults = SaveDefaults.CreateSettings();
            if (data == null)
            {
                return defaults;
            }

            int sourceSchemaVersion = data.schemaVersion;
            data.schemaVersion = SaveDefaults.SettingsSchemaVersion;
            data.defaultDetection = NormalizeEnum(
                data.defaultDetection,
                RuntimeDetectionMode.Onset).ToString();
            data.defaultDifficulty = NormalizeEnum(
                data.defaultDifficulty,
                RuntimeDifficulty.Normal).ToString();
            data.defaultCombatStyle = NormalizeEnum(
                data.defaultCombatStyle,
                RuntimeCombatStyle.Legacy).ToString();
            data.beatmapOffsetSeconds = NormalizeFinite(
                data.beatmapOffsetSeconds,
                defaults.beatmapOffsetSeconds,
                MaximumBeatmapOffsetSeconds);
            data.inputTimingOffsetSeconds = NormalizeFinite(
                data.inputTimingOffsetSeconds,
                defaults.inputTimingOffsetSeconds,
                MaximumInputOffsetSeconds);
            data.audio = data.audio ?? defaults.audio;
            if (sourceSchemaVersion < 2)
            {
                data.audio.musicVolume = defaults.audio.musicVolume;
            }
            data.audio.masterVolume = Mathf.Clamp01(IsFinite(data.audio.masterVolume)
                ? data.audio.masterVolume
                : defaults.audio.masterVolume);
            data.audio.musicVolume = Mathf.Clamp01(IsFinite(data.audio.musicVolume)
                ? data.audio.musicVolume
                : defaults.audio.musicVolume);
            data.display = data.display ?? defaults.display;
            if (sourceSchemaVersion < 2)
            {
                data.display.displayMode = defaults.display.displayMode;
                data.display.resolutionWidth = defaults.display.resolutionWidth;
                data.display.resolutionHeight = defaults.display.resolutionHeight;
                data.display.refreshRate = defaults.display.refreshRate;
                data.display.vSync = defaults.display.vSync;
                data.display.frameRateLimit = defaults.display.frameRateLimit;
            }
            data.display.displayMode = NormalizeEnum(
                data.display.displayMode,
                PulseForgeDisplayMode.Windowed).ToString();
            NormalizeResolution(data.display, defaults.display);
            data.display.frameRateLimit = IsSupportedFrameRate(data.display.frameRateLimit)
                ? data.display.frameRateLimit
                : defaults.display.frameRateLimit;
            data.input = data.input ?? defaults.input;
            if (sourceSchemaVersion < 2)
            {
                data.input.inputBindingOverridesJson = defaults.input.inputBindingOverridesJson;
            }
            data.input.reservedBindingProfile = data.input.reservedBindingProfile ?? string.Empty;
            data.input.inputBindingOverridesJson = data.input.inputBindingOverridesJson ?? string.Empty;
            if (data.input.inputBindingOverridesJson.Length > MaximumBindingOverridesJsonLength)
            {
                data.input.inputBindingOverridesJson = string.Empty;
            }

            return data;
        }

        private static void NormalizeResolution(
            PulseForgeDisplaySettingsData display,
            PulseForgeDisplaySettingsData defaults)
        {
            if (display.resolutionWidth < 640
                || display.resolutionHeight < 360
                || display.refreshRate <= 0
                || !ResolutionExists(
                    display.resolutionWidth,
                    display.resolutionHeight,
                    display.refreshRate))
            {
                display.resolutionWidth = defaults.resolutionWidth;
                display.resolutionHeight = defaults.resolutionHeight;
                display.refreshRate = defaults.refreshRate;
            }
        }

        private static bool ResolutionExists(int width, int height, int refreshRate)
        {
            Resolution[] resolutions = Screen.resolutions;
            if (resolutions == null || resolutions.Length == 0)
            {
                return width >= 640 && height >= 360 && refreshRate > 0;
            }

            for (int i = 0; i < resolutions.Length; i++)
            {
                Resolution resolution = resolutions[i];
                int availableRefreshRate = Math.Max(
                    1,
                    (int)Math.Round(resolution.refreshRateRatio.value));
                if (resolution.width == width
                    && resolution.height == height
                    && availableRefreshRate == refreshRate)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSupportedFrameRate(int value)
        {
            return value == -1
                || value == 60
                || value == 120
                || value == 144
                || value == 165
                || value == 240;
        }

        public static PulseForgeProfileData NormalizeProfile(PulseForgeProfileData data)
        {
            if (data == null)
            {
                return SaveDefaults.CreateProfile();
            }

            data.schemaVersion = SaveDefaults.ProfileSchemaVersion;
            data.completedSessions = Math.Max(0L, data.completedSessions);
            data.totalPerfect = Math.Max(0L, data.totalPerfect);
            data.totalGood = Math.Max(0L, data.totalGood);
            data.totalMiss = Math.Max(0L, data.totalMiss);
            data.highestScore = Math.Max(0, data.highestScore);
            data.highestCombo = Math.Max(0, data.highestCombo);
            data.lastPlayedAtUtc = NormalizeOptionalUtc(data.lastPlayedAtUtc);
            return data;
        }

        public static SavedTrackLibraryData NormalizeLibrary(SavedTrackLibraryData data)
        {
            if (data == null)
            {
                return SaveDefaults.CreateLibrary();
            }

            data.schemaVersion = SaveDefaults.LibrarySchemaVersion;
            data.tracks = data.tracks ?? new List<SavedTrackData>();
            HashSet<string> trackIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = data.tracks.Count - 1; i >= 0; i--)
            {
                SavedTrackData track = data.tracks[i];
                if (track == null || string.IsNullOrWhiteSpace(track.trackId)
                    || !trackIds.Add(track.trackId.Trim()))
                {
                    data.tracks.RemoveAt(i);
                    continue;
                }

                NormalizeTrack(track);
            }

            return data;
        }

        public static bool TryGetPipelineSettings(
            string detection,
            string difficulty,
            string combatStyle,
            out RuntimeAudioPipelineSettings settings)
        {
            if (!Enum.TryParse(detection, true, out RuntimeDetectionMode detectionMode)
                || !Enum.TryParse(difficulty, true, out RuntimeDifficulty difficultyMode)
                || !Enum.TryParse(combatStyle, true, out RuntimeCombatStyle combatStyleMode))
            {
                settings = RuntimeAudioPipelineSettings.Default;
                return false;
            }

            settings = new RuntimeAudioPipelineSettings(
                detectionMode,
                difficultyMode,
                combatStyleMode);
            return true;
        }

        public static string PresetKey(
            string detection,
            string difficulty,
            string combatStyle)
        {
            return (detection ?? string.Empty).Trim().ToUpperInvariant() + "|"
                + (difficulty ?? string.Empty).Trim().ToUpperInvariant() + "|"
                + (combatStyle ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static void NormalizeTrack(SavedTrackData track)
        {
            track.trackId = track.trackId.Trim().ToLowerInvariant();
            track.contentHash = string.IsNullOrWhiteSpace(track.contentHash)
                ? track.trackId
                : track.contentHash.Trim().ToLowerInvariant();
            track.originalFilePath = track.originalFilePath ?? string.Empty;
            track.originalFileName = string.IsNullOrWhiteSpace(track.originalFileName)
                ? Path.GetFileName(track.originalFilePath)
                : track.originalFileName.Trim();
            track.displayName = string.IsNullOrWhiteSpace(track.displayName)
                ? SafeFileName(track.originalFilePath)
                : track.displayName.Trim();
            track.sourceExtension = string.IsNullOrWhiteSpace(track.sourceExtension)
                ? Path.GetExtension(track.originalFilePath).TrimStart('.').ToUpperInvariant()
                : track.sourceExtension.Trim().TrimStart('.').ToUpperInvariant();
            track.fileSizeBytes = Math.Max(0L, track.fileSizeBytes);
            track.durationSeconds = IsFinite(track.durationSeconds)
                ? Math.Max(0d, track.durationSeconds)
                : 0d;
            track.fileMissing = string.IsNullOrWhiteSpace(track.originalFilePath)
                || !File.Exists(track.originalFilePath);
            track.cachedAudioRelativePath = NormalizeRelativeCachePath(track.cachedAudioRelativePath);
            track.cacheVersion = Math.Max(0, track.cacheVersion);
            string now = SaveDefaults.UtcNow();
            track.createdAtUtc = NormalizeRequiredUtc(track.createdAtUtc, now);
            track.updatedAtUtc = NormalizeRequiredUtc(track.updatedAtUtc, track.createdAtUtc);
            track.lastUsedAtUtc = NormalizeRequiredUtc(track.lastUsedAtUtc, track.createdAtUtc);
            track.presets = track.presets ?? new List<SavedTrackPresetData>();

            Dictionary<string, SavedTrackPresetData> presets =
                new Dictionary<string, SavedTrackPresetData>(StringComparer.Ordinal);
            for (int i = track.presets.Count - 1; i >= 0; i--)
            {
                SavedTrackPresetData preset = track.presets[i];
                if (preset == null)
                {
                    track.presets.RemoveAt(i);
                    continue;
                }

                NormalizePreset(preset, track.createdAtUtc);
                string key = PresetKey(preset.detectionMode, preset.difficulty, preset.combatStyle);
                if (presets.TryGetValue(key, out SavedTrackPresetData existing))
                {
                    MergePreset(existing, preset);
                    track.presets.RemoveAt(i);
                }
                else
                {
                    presets.Add(key, preset);
                }
            }
        }

        private static void NormalizePreset(SavedTrackPresetData preset, string fallbackUtc)
        {
            preset.presetId = string.IsNullOrWhiteSpace(preset.presetId)
                ? Guid.NewGuid().ToString("N")
                : preset.presetId.Trim();
            preset.detectionMode = NormalizeEnum(
                preset.detectionMode,
                RuntimeDetectionMode.Onset).ToString();
            preset.difficulty = NormalizeEnum(
                preset.difficulty,
                RuntimeDifficulty.Normal).ToString();
            preset.combatStyle = NormalizeEnum(
                preset.combatStyle,
                RuntimeCombatStyle.Legacy).ToString();
            preset.eventCount = Math.Max(0, preset.eventCount);
            preset.createdAtUtc = NormalizeRequiredUtc(preset.createdAtUtc, fallbackUtc);
            preset.updatedAtUtc = NormalizeRequiredUtc(preset.updatedAtUtc, preset.createdAtUtc);
            preset.lastPlayedAtUtc = NormalizeOptionalUtc(preset.lastPlayedAtUtc);
            preset.playCount = Math.Max(0, preset.playCount);
            preset.bestScore = Math.Max(0, preset.bestScore);
            preset.maxCombo = Math.Max(0, preset.maxCombo);
            preset.bestPerfectCount = Math.Max(0, preset.bestPerfectCount);
            preset.bestGoodCount = Math.Max(0, preset.bestGoodCount);
            preset.lowestMissCount = Math.Max(0, preset.lowestMissCount);
            preset.cachedBeatmapRelativePath = NormalizeRelativeCachePath(
                preset.cachedBeatmapRelativePath);
            preset.cacheVersion = Math.Max(0, preset.cacheVersion);
            preset.cacheStatus = NormalizeCacheStatus(preset);
        }

        public static SavedTrackCacheStatus GetCacheStatus(SavedTrackPresetData preset)
        {
            if (preset != null
                && Enum.TryParse(preset.cacheStatus, true, out SavedTrackCacheStatus status)
                && Enum.IsDefined(typeof(SavedTrackCacheStatus), status))
            {
                return status;
            }

            return SavedTrackCacheStatus.NeedsRebuild;
        }

        private static void MergePreset(SavedTrackPresetData target, SavedTrackPresetData source)
        {
            bool targetHadPerformance = target.playCount > 0;
            bool sourceHadPerformance = source.playCount > 0;
            target.eventCount = Math.Max(target.eventCount, source.eventCount);
            target.playCount += source.playCount;
            target.bestScore = Math.Max(target.bestScore, source.bestScore);
            target.maxCombo = Math.Max(target.maxCombo, source.maxCombo);
            target.bestPerfectCount = Math.Max(target.bestPerfectCount, source.bestPerfectCount);
            target.bestGoodCount = Math.Max(target.bestGoodCount, source.bestGoodCount);
            if (!targetHadPerformance && sourceHadPerformance)
            {
                target.lowestMissCount = source.lowestMissCount;
            }
            else if (targetHadPerformance && sourceHadPerformance)
            {
                target.lowestMissCount = Math.Min(target.lowestMissCount, source.lowestMissCount);
            }

            if (string.CompareOrdinal(source.updatedAtUtc, target.updatedAtUtc) > 0)
            {
                target.updatedAtUtc = source.updatedAtUtc;
            }

            if (source.cacheVersion > 0
                && !string.IsNullOrWhiteSpace(source.cachedBeatmapRelativePath)
                && (target.cacheVersion <= 0
                    || string.CompareOrdinal(source.updatedAtUtc, target.updatedAtUtc) >= 0))
            {
                target.cachedBeatmapRelativePath = source.cachedBeatmapRelativePath;
                target.cacheVersion = source.cacheVersion;
                target.cacheStatus = source.cacheStatus;
            }

            if (string.CompareOrdinal(source.lastPlayedAtUtc, target.lastPlayedAtUtc) > 0)
            {
                target.lastPlayedAtUtc = source.lastPlayedAtUtc;
            }
        }

        private static T NormalizeEnum<T>(string value, T fallback) where T : struct
        {
            return Enum.TryParse(value, true, out T parsed) && Enum.IsDefined(typeof(T), parsed)
                ? parsed
                : fallback;
        }

        private static float NormalizeFinite(float value, float fallback, float absoluteMaximum)
        {
            return IsFinite(value)
                ? Mathf.Clamp(value, -absoluteMaximum, absoluteMaximum)
                : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static string NormalizeRequiredUtc(string value, string fallback)
        {
            return DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTime parsed)
                    ? parsed.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
                    : fallback;
        }

        private static string NormalizeOptionalUtc(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : NormalizeRequiredUtc(value, string.Empty);
        }

        private static string SafeFileName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "Saved Track";
            }

            string fileName = Path.GetFileNameWithoutExtension(path);
            return string.IsNullOrWhiteSpace(fileName) ? "Saved Track" : fileName;
        }

        private static string NormalizeCacheStatus(SavedTrackPresetData preset)
        {
            if (preset.cacheVersion <= 0
                || string.IsNullOrWhiteSpace(preset.cachedBeatmapRelativePath))
            {
                return SavedTrackCacheStatus.NeedsRebuild.ToString();
            }

            return Enum.TryParse(preset.cacheStatus, true, out SavedTrackCacheStatus status)
                && Enum.IsDefined(typeof(SavedTrackCacheStatus), status)
                    ? status.ToString()
                    : SavedTrackCacheStatus.Damaged.ToString();
        }

        private static string NormalizeRelativeCachePath(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace('\\', '/').TrimStart('/');
        }
    }
}
