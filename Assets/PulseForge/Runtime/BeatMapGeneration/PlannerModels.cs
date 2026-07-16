using System;
using System.Collections.Generic;
using PulseForge.AudioAnalysis;
using PulseForge.Domain.Rhythm;

namespace PulseForge.BeatMapGeneration
{
    public enum BeatMapDifficulty
    {
        Easy,
        Normal,
        Hard
    }

    public enum CombatStyle
    {
        Legacy,
        Balanced,
        Defensive,
        Aggressive,
        Bursty
    }

    public enum CoverageMode
    {
        Relaxed,
        Standard,
        FullPulse
    }

    public enum PlannerQualityResult
    {
        Pass,
        PassWithRepairs,
        UnderCovered,
        RhythmAlignmentLow
    }

    [Serializable]
    public sealed class RadialEncounterPlanResult
    {
        public RadialBeatMapData beatMap = new RadialBeatMapData();
        public PlannerQualityReport qualityReport = new PlannerQualityReport();
    }

    [Serializable]
    public sealed class PlannerQualityReport
    {
        public int totalInputCost;
        public double activeDurationSeconds;
        public double overallDensity;
        public double longestActiveGapSeconds;
        public int onsetInputCost;
        public int gridFillInputCost;
        public double onsetToGridFillRatio;
        public List<SectionDensityReport> sectionDensities = new List<SectionDensityReport>();
        public List<ActionCountData> actionCounts = new List<ActionCountData>();
        public List<EventTypeCountData> eventTypeCounts = new List<EventTypeCountData>();
        public List<EnemyArchetypeCountData> enemyArchetypeCounts = new List<EnemyArchetypeCountData>();
        public int sweepCount;
        public int compoundEventCount;
        public int saboteurEncounterCount;
        public int fogFailureEffectCount;
        public double totalFogDurationSeconds;
        public double minimumFogDurationSeconds;
        public double maximumFogDurationSeconds;
        public CoverageMode coverageMode;
        public double beatAlignedRequirementRatio;
        public double averageGridDeviationSeconds;
        public double maximumGridDeviationSeconds;
        public int offGridRequirementCount;
        public int activeGridPointCount;
        public int usedGridPointCount;
        public double usedGridPointRatio;
        public double compoundEventRatio;
        public int angularRepairCount;
        public PlannerQualityResult coverageResult;
        public List<string> warnings = new List<string>();
        public List<string> repairReasons = new List<string>();
        public List<string> dropReasons = new List<string>();
        public PlannerQualityResult result;
    }

    [Serializable]
    public sealed class SectionDensityReport
    {
        public double startTimeSeconds;
        public double endTimeSeconds;
        public SongSectionActivityLevel activityLevel;
        public int inputCost;
        public double density;
        public double longestGapSeconds;
    }

    [Serializable]
    public sealed class ActionCountData
    {
        public RhythmAction action;
        public int count;
    }

    [Serializable]
    public sealed class EventTypeCountData
    {
        public RadialEventType eventType;
        public int count;
    }

    [Serializable]
    public sealed class EnemyArchetypeCountData
    {
        public EnemyArchetype archetype;
        public int count;
    }

    [Serializable]
    public sealed class BeatMapValidationReport
    {
        public List<string> repairReasons = new List<string>();
        public List<string> dropReasons = new List<string>();
        public bool underCovered;
        public int angularRepairCount;
    }

    internal sealed class PlannedCue
    {
        public double TimeSeconds;
        public double Confidence;
        public AudioBandMask Bands;
        public bool IsGridFill;
        public int SectionIndex;
    }

    internal static class PlannerRules
    {
        public static CoverageMode DefaultCoverage(BeatMapDifficulty difficulty)
        {
            return difficulty == BeatMapDifficulty.Easy
                ? CoverageMode.Relaxed
                : CoverageMode.Standard;
        }

        public static double MinimumDensity(CoverageMode coverage)
        {
            switch (coverage)
            {
                case CoverageMode.Relaxed:
                    return 0.6d;
                case CoverageMode.FullPulse:
                    return 1.2d;
                default:
                    return 0.9d;
            }
        }

        public static double MaximumDensity(CoverageMode coverage)
        {
            switch (coverage)
            {
                case CoverageMode.Relaxed:
                    return 1d;
                case CoverageMode.FullPulse:
                    return 2.2d;
                default:
                    return 1.5d;
            }
        }

        public static double MaximumGap(CoverageMode coverage)
        {
            switch (coverage)
            {
                case CoverageMode.Relaxed:
                    return 3d;
                case CoverageMode.FullPulse:
                    return 1d;
                default:
                    return 2.2d;
            }
        }

        public static double MaximumCompoundRatio(
            BeatMapDifficulty difficulty,
            CoverageMode coverage)
        {
            if (coverage == CoverageMode.FullPulse)
            {
                return 0.10d;
            }
            switch (difficulty)
            {
                case BeatMapDifficulty.Easy:
                    return 0.05d;
                case BeatMapDifficulty.Hard:
                    return 0.22d;
                default:
                    return 0.12d;
            }
        }

        public static double MinimumDensity(BeatMapDifficulty difficulty)
        {
            return MinimumDensity(DefaultCoverage(difficulty));
        }

        public static double MaximumDensity(BeatMapDifficulty difficulty)
        {
            return MaximumDensity(DefaultCoverage(difficulty));
        }

        public static double MaximumGap(BeatMapDifficulty difficulty)
        {
            return MaximumGap(DefaultCoverage(difficulty));
        }

        public static double MinimumRecovery(BeatMapDifficulty difficulty)
        {
            switch (difficulty)
            {
                case BeatMapDifficulty.Easy:
                    return 0.30d;
                case BeatMapDifficulty.Hard:
                    return 0.16d;
                default:
                    return 0.22d;
            }
        }
    }
}
