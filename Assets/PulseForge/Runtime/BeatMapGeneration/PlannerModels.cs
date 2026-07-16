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

    public enum PlannerQualityResult
    {
        Pass,
        PassWithRepairs,
        UnderCovered
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
        public static double MinimumDensity(BeatMapDifficulty difficulty)
        {
            switch (difficulty)
            {
                case BeatMapDifficulty.Easy:
                    return 0.6d;
                case BeatMapDifficulty.Hard:
                    return 1.2d;
                default:
                    return 0.9d;
            }
        }

        public static double MaximumDensity(BeatMapDifficulty difficulty)
        {
            switch (difficulty)
            {
                case BeatMapDifficulty.Easy:
                    return 1d;
                case BeatMapDifficulty.Hard:
                    return 2d;
                default:
                    return 1.5d;
            }
        }

        public static double MaximumGap(BeatMapDifficulty difficulty)
        {
            switch (difficulty)
            {
                case BeatMapDifficulty.Easy:
                    return 3d;
                case BeatMapDifficulty.Hard:
                    return 1.5d;
                default:
                    return 2.2d;
            }
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
