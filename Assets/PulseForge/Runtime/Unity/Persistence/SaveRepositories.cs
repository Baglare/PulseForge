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
            return RecordCompletedSession(snapshot, true);
        }

        public bool RecordCompletedSession(ScoreSnapshot snapshot, bool compareWithLegacyBest)
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
            if (compareWithLegacyBest)
            {
                Current.highestScore = Math.Max(Current.highestScore, snapshot.TotalScore);
                Current.highestCombo = Math.Max(Current.highestCombo, snapshot.MaxCombo);
            }
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
            string presetId,
            string cachedAudioRelativePath,
            string cachedBeatmapRelativePath,
            int cacheVersion,
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
                    originalFileName = metadata.OriginalFileName,
                    originalFilePath = metadata.OriginalFilePath,
                    sourceExtension = metadata.SourceExtension,
                    fileSizeBytes = metadata.FileSizeBytes,
                    durationSeconds = metadata.DurationSeconds,
                    contentHash = metadata.ContentHash,
                    fileMissing = false,
                    cachedAudioRelativePath = cachedAudioRelativePath,
                    cacheVersion = cacheVersion,
                    createdAtUtc = now,
                    updatedAtUtc = now,
                    lastUsedAtUtc = now,
                    presets = new System.Collections.Generic.List<SavedTrackPresetData>()
                };
                Current.tracks.Add(track);
            }
            else
            {
                track.displayName = metadata.DisplayName;
                track.originalFileName = metadata.OriginalFileName;
                track.originalFilePath = metadata.OriginalFilePath;
                track.sourceExtension = metadata.SourceExtension;
                track.fileSizeBytes = metadata.FileSizeBytes;
                track.durationSeconds = metadata.DurationSeconds;
                track.contentHash = metadata.ContentHash;
                track.fileMissing = false;
                track.cachedAudioRelativePath = cachedAudioRelativePath;
                track.cacheVersion = cacheVersion;
                track.updatedAtUtc = now;
                track.lastUsedAtUtc = now;
            }

            SavedTrackPresetData preset = FindPresetBySettings(track, settings);
            if (preset == null)
            {
                preset = new SavedTrackPresetData
                {
                    presetId = string.IsNullOrWhiteSpace(presetId)
                        ? Guid.NewGuid().ToString("N")
                        : presetId,
                    detectionMode = settings.DetectionMode.ToString(),
                    difficulty = settings.Difficulty.ToString(),
                    combatStyle = settings.CombatStyle.ToString(),
                    coverage = settings.Coverage.ToString(),
                    eventCount = Math.Max(0, eventCount),
                    createdAtUtc = now,
                    updatedAtUtc = now,
                    lastPlayedAtUtc = string.Empty,
                    cachedBeatmapRelativePath = cachedBeatmapRelativePath,
                    cacheVersion = cacheVersion,
                    cacheStatus = SavedTrackCacheStatus.Ready.ToString()
                };
                track.presets.Add(preset);
            }
            else
            {
                preset.eventCount = Math.Max(0, eventCount);
                preset.updatedAtUtc = now;
                preset.cachedBeatmapRelativePath = cachedBeatmapRelativePath;
                preset.cacheVersion = cacheVersion;
                preset.cacheStatus = SavedTrackCacheStatus.Ready.ToString();
            }

            reference = new SavedTrackPresetReference(track.trackId, preset.presetId);
            return Save();
        }

        internal bool AddOrUpdateRadialTrackPreset(
            SavedTrackMetadata metadata,
            RuntimeAudioPipelineSettings settings,
            int eventCount,
            int inputCost,
            string plannerResult,
            string presetId,
            string cachedAudioRelativePath,
            string cachedBeatmapRelativePath,
            string beatMapFingerprint,
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
                    createdAtUtc = now,
                    presets = new System.Collections.Generic.List<SavedTrackPresetData>()
                };
                Current.tracks.Add(track);
            }

            track.displayName = metadata.DisplayName;
            track.originalFileName = metadata.OriginalFileName;
            track.originalFilePath = metadata.OriginalFilePath;
            track.sourceExtension = metadata.SourceExtension;
            track.fileSizeBytes = metadata.FileSizeBytes;
            track.durationSeconds = metadata.DurationSeconds;
            track.contentHash = metadata.ContentHash;
            track.fileMissing = false;
            track.cachedAudioRelativePath = cachedAudioRelativePath;
            track.cacheVersion = SaveDefaults.AudioCacheVersion;
            track.audioCacheVersion = SaveDefaults.AudioCacheVersion;
            track.updatedAtUtc = now;
            track.lastUsedAtUtc = now;

            SavedTrackPresetData preset = FindPresetBySettings(
                track,
                settings,
                SaveDefaults.AnalyzerVersion,
                true);
            if (preset == null)
            {
                preset = new SavedTrackPresetData
                {
                    presetId = string.IsNullOrWhiteSpace(presetId)
                        ? Guid.NewGuid().ToString("N")
                        : presetId,
                    createdAtUtc = now,
                    lastPlayedAtUtc = string.Empty,
                    performances = new System.Collections.Generic.List<SavedTrackPerformanceData>()
                };
                track.presets.Add(preset);
            }

            ApplyRadialPresetMetadata(
                preset,
                settings,
                eventCount,
                inputCost,
                plannerResult,
                cachedBeatmapRelativePath,
                beatMapFingerprint,
                now);
            reference = new SavedTrackPresetReference(track.trackId, preset.presetId);
            return Save();
        }

        internal bool UpdateRebuiltRadialPreset(
            string trackId,
            string presetId,
            RuntimeAudioPipelineSettings settings,
            int eventCount,
            int inputCost,
            string plannerResult,
            string cachedAudioRelativePath,
            string cachedBeatmapRelativePath,
            string beatMapFingerprint)
        {
            Current = SaveDataNormalizer.NormalizeLibrary(Current);
            SavedTrackData track = FindTrack(trackId);
            SavedTrackPresetData preset = FindPreset(track, presetId);
            if (track == null || preset == null)
            {
                return false;
            }

            string now = SaveDefaults.UtcNow();
            track.cachedAudioRelativePath = cachedAudioRelativePath;
            track.cacheVersion = SaveDefaults.AudioCacheVersion;
            track.audioCacheVersion = SaveDefaults.AudioCacheVersion;
            track.updatedAtUtc = now;
            track.lastUsedAtUtc = now;
            ApplyRadialPresetMetadata(
                preset,
                settings,
                eventCount,
                inputCost,
                plannerResult,
                cachedBeatmapRelativePath,
                beatMapFingerprint,
                now);
            return Save();
        }

        public bool RecordPerformance(
            string trackId,
            string presetId,
            ScoreSnapshot snapshot)
        {
            return RecordPerformance(
                trackId,
                presetId,
                snapshot,
                ScoreSchema.LegacyV1,
                string.Empty,
                RadialGameMode.Standard,
                TimingAssistMode.Standard,
                RadialRunOutcome.Clear);
        }

        public bool RecordPerformance(
            string trackId,
            string presetId,
            ScoreSnapshot snapshot,
            string scoreSchema,
            string beatMapFingerprint)
        {
            return RecordPerformance(
                trackId,
                presetId,
                snapshot,
                scoreSchema,
                beatMapFingerprint,
                RadialGameMode.Standard,
                TimingAssistMode.Standard,
                RadialRunOutcome.Clear);
        }

        public bool RecordPerformance(
            string trackId,
            string presetId,
            ScoreSnapshot snapshot,
            string scoreSchema,
            string beatMapFingerprint,
            RadialGameMode gameMode,
            RadialRunOutcome outcome)
        {
            return RecordPerformance(
                trackId,
                presetId,
                snapshot,
                scoreSchema,
                beatMapFingerprint,
                gameMode,
                TimingAssistMode.Standard,
                outcome);
        }

        public bool RecordPerformance(
            string trackId,
            string presetId,
            ScoreSnapshot snapshot,
            string scoreSchema,
            string beatMapFingerprint,
            RadialGameMode gameMode,
            TimingAssistMode timingAssist,
            RadialRunOutcome outcome)
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

            string now = SaveDefaults.UtcNow();
            scoreSchema = string.IsNullOrWhiteSpace(scoreSchema)
                ? ScoreSchema.LegacyV1
                : scoreSchema.Trim();
            beatMapFingerprint = (beatMapFingerprint ?? string.Empty).Trim().ToLowerInvariant();
            preset.performances = preset.performances
                ?? new System.Collections.Generic.List<SavedTrackPerformanceData>();
            SavedTrackPerformanceData performance = null;
            for (int i = 0; i < preset.performances.Count; i++)
            {
                SavedTrackPerformanceData candidate = preset.performances[i];
                if (candidate != null && SaveDataNormalizer.CanComparePerformance(
                    candidate.gameMode,
                    candidate.timingAssist,
                    candidate.scoreSchema,
                    candidate.beatMapFingerprint,
                    gameMode.ToString(),
                    timingAssist.ToString(),
                    scoreSchema,
                    beatMapFingerprint))
                {
                    performance = candidate;
                    break;
                }
            }

            if (performance == null)
            {
                performance = new SavedTrackPerformanceData
                {
                    gameMode = gameMode.ToString(),
                    timingAssist = timingAssist.ToString(),
                    scoreSchema = scoreSchema,
                    beatMapFingerprint = beatMapFingerprint
                };
                preset.performances.Add(performance);
            }

            bool isFirstPerformance = performance.attemptCount == 0;
            performance.attemptCount++;
            performance.playCount = performance.attemptCount;
            if (outcome == RadialRunOutcome.Clear)
            {
                performance.clearCount++;
            }
            performance.bestScore = Math.Max(performance.bestScore, snapshot.TotalScore);
            performance.maxCombo = Math.Max(performance.maxCombo, snapshot.MaxCombo);
            performance.bestPerfectCount = Math.Max(
                performance.bestPerfectCount,
                snapshot.PerfectCount);
            performance.bestGoodCount = Math.Max(performance.bestGoodCount, snapshot.GoodCount);
            performance.lowestMissCount = isFirstPerformance
                ? snapshot.MissCount
                : Math.Min(performance.lowestMissCount, snapshot.MissCount);
            performance.lastOutcome = outcome.ToString();
            performance.lastPlayedAtUtc = now;

            if (gameMode == RadialGameMode.Standard
                && timingAssist == TimingAssistMode.Standard
                && string.Equals(scoreSchema, ScoreSchema.LegacyV1, StringComparison.Ordinal))
            {
                preset.playCount = performance.playCount;
                preset.bestScore = performance.bestScore;
                preset.maxCombo = performance.maxCombo;
                preset.bestPerfectCount = performance.bestPerfectCount;
                preset.bestGoodCount = performance.bestGoodCount;
                preset.lowestMissCount = performance.lowestMissCount;
            }
            preset.lastPlayedAtUtc = now;
            preset.updatedAtUtc = now;
            track.updatedAtUtc = now;
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
            track.updatedAtUtc = SaveDefaults.UtcNow();
            track.lastUsedAtUtc = track.updatedAtUtc;
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

        public bool RemovePreset(string trackId, string presetId, out bool removedTrack)
        {
            removedTrack = false;
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
                    if (track.presets.Count == 0)
                    {
                        Current.tracks.Remove(track);
                        removedTrack = true;
                    }

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

        internal SavedTrackPresetData FindPresetBySettings(
            SavedTrackData track,
            RuntimeAudioPipelineSettings settings)
        {
            return FindPresetBySettings(track, settings, 0, false);
        }

        internal SavedTrackPresetData FindPresetBySettings(
            SavedTrackData track,
            RuntimeAudioPipelineSettings settings,
            int analyzerVersion,
            bool includeLegacyFallback)
        {
            if (track == null || track.presets == null)
            {
                return null;
            }

            string expected = SaveDataNormalizer.PresetKey(
                settings.DetectionMode.ToString(),
                settings.Difficulty.ToString(),
                settings.CombatStyle.ToString(),
                settings.Coverage.ToString(),
                analyzerVersion,
                analyzerVersion > 0 ? SaveDefaults.PlannerVersion : 0);
            SavedTrackPresetData legacyFallback = null;
            for (int i = 0; i < track.presets.Count; i++)
            {
                SavedTrackPresetData preset = track.presets[i];
                string actual = SaveDataNormalizer.PresetKey(
                    preset.detectionMode,
                    preset.difficulty,
                    preset.combatStyle,
                    preset.coverage,
                    preset.analyzerVersion,
                    preset.plannerVersion);
                if (string.Equals(expected, actual, StringComparison.Ordinal))
                {
                    return preset;
                }

                if (includeLegacyFallback && preset.analyzerVersion <= 0)
                {
                    string legacyActual = SaveDataNormalizer.PresetKey(
                        preset.detectionMode,
                        preset.difficulty,
                        preset.combatStyle,
                        preset.coverage,
                        analyzerVersion,
                        SaveDefaults.PlannerVersion);
                    if (string.Equals(expected, legacyActual, StringComparison.Ordinal))
                    {
                        legacyFallback = preset;
                    }
                }
            }

            return legacyFallback;
        }

        public bool SaveCacheStatuses()
        {
            return Save();
        }

        public SavedTrackLibraryData Clear(out bool saved)
        {
            Current = SaveDefaults.CreateLibrary();
            saved = Save();
            return Current;
        }

        private bool Save()
        {
            Current = SaveDataNormalizer.NormalizeLibrary(Current);
            return store.Save(FileName, Current, SaveDataNormalizer.NormalizeLibrary);
        }

        private static void ApplyRadialPresetMetadata(
            SavedTrackPresetData preset,
            RuntimeAudioPipelineSettings settings,
            int eventCount,
            int inputCost,
            string plannerResult,
            string cachedBeatmapRelativePath,
            string beatMapFingerprint,
            string now)
        {
            preset.detectionMode = settings.DetectionMode.ToString();
            preset.difficulty = settings.Difficulty.ToString();
            preset.combatStyle = settings.CombatStyle.ToString();
            preset.coverage = settings.Coverage.ToString();
            preset.eventCount = Math.Max(0, eventCount);
            preset.inputCost = Math.Max(0, inputCost);
            preset.plannerResult = plannerResult ?? string.Empty;
            preset.updatedAtUtc = now;
            preset.cachedBeatmapRelativePath = cachedBeatmapRelativePath;
            preset.cacheVersion = SaveDefaults.RadialBeatMapCacheVersion;
            preset.beatMapCacheVersion = SaveDefaults.RadialBeatMapCacheVersion;
            preset.analyzerVersion = SaveDefaults.AnalyzerVersion;
            preset.plannerVersion = SaveDefaults.PlannerVersion;
            preset.beatMapFingerprint = (beatMapFingerprint ?? string.Empty)
                .Trim()
                .ToLowerInvariant();
            preset.cacheStatus = SavedTrackCacheStatus.Ready.ToString();
        }
    }
}
