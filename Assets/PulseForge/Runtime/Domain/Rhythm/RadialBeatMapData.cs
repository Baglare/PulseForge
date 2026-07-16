using System;
using System.Collections.Generic;

namespace PulseForge.Domain.Rhythm
{
    public static class RadialTimingDefaults
    {
        public const double PerfectWindowSeconds = 0.045d;
        public const double GoodWindowSeconds = 0.100d;
    }

    public enum RadialEventType
    {
        Tap,
        GuardHold,
        HeavyChargeRelease,
        Chord,
        Choice,
        OrderedSequence,
        TimedChain,
        SwarmChain,
        BreakTarget,
        Sweep
    }

    public enum InputGestureType
    {
        Tap,
        Hold,
        Charge,
        Chord,
        Choice,
        SequenceStep,
        ChainStep,
        RepeatedPress
    }

    public enum RhythmInputPhase
    {
        Pressed,
        Released
    }

    public enum RadialDirection
    {
        North,
        NorthEast,
        East,
        SouthEast,
        South,
        SouthWest,
        West,
        NorthWest
    }

    public enum EnemyArchetype
    {
        Duelist,
        Brute,
        Raider,
        Armored,
        ArcherGunner,
        Swarm,
        GiantBreaker,
        Saboteur
    }

    public enum FailureEffectType
    {
        None,
        Fog
    }

    [Flags]
    public enum RhythmActionMask
    {
        None = 0,
        Guard = 1 << 0,
        LightAttack = 1 << 1,
        Dodge = 1 << 2,
        HeavyAttack = 1 << 3
    }

    [Serializable]
    public sealed class RadialBeatMapData
    {
        public int schemaVersion = 3;
        public string displayName = string.Empty;
        public double globalOffsetSeconds;
        public List<RadialEncounterEventData> encounters = new List<RadialEncounterEventData>();
    }

    [Serializable]
    public sealed class RadialEncounterEventData
    {
        public string eventId = string.Empty;
        public RadialEventType eventType;
        public float intensity = 1f;
        public double telegraphLeadSeconds;
        public double perfectSpreadSeconds = RadialTimingDefaults.PerfectWindowSeconds;
        public double goodSpreadSeconds = RadialTimingDefaults.GoodWindowSeconds;
        public FailureEffectData failureEffect = new FailureEffectData();
        public List<InputRequirementData> requirements = new List<InputRequirementData>();
        public List<EncounterTargetData> targets = new List<EncounterTargetData>();
    }

    [Serializable]
    public sealed class FailureEffectData
    {
        public FailureEffectType effectType;
        public double durationSeconds;
        public float revealLeadMultiplier = 1f;
        public double minimumVisibleLeadSeconds;
    }

    [Serializable]
    public sealed class InputRequirementData
    {
        public string requirementId = string.Empty;
        public RhythmActionMask acceptedActions;
        public InputGestureType gestureType = InputGestureType.Tap;
        public RhythmInputPhase phase = RhythmInputPhase.Pressed;
        public double targetTimeSeconds;
        public double perfectWindowSeconds = RadialTimingDefaults.PerfectWindowSeconds;
        public double goodWindowSeconds = RadialTimingDefaults.GoodWindowSeconds;
        public int orderIndex;
        public bool isOptional;
        public bool exclusive = true;

        public double holdEndTimeSeconds;
        public double earlyReleaseGraceSeconds;
        public bool allowEarlyReleaseAsGood;
        public bool autoCompleteAtHoldEnd = true;

        public string pairedRequirementId = string.Empty;
        public double minimumHoldSeconds;
        public double maximumHoldSeconds;

        public double windowStartTimeSeconds;
        public double perfectDeadlineSeconds;
        public double goodDeadlineSeconds;
        public int requiredPressCount;
        public double minimumPressIntervalSeconds;
    }

    [Serializable]
    public sealed class EncounterTargetData
    {
        public string targetId = string.Empty;
        public string requirementId = string.Empty;
        public RadialDirection direction;
        public EnemyArchetype archetype;
    }

    public static class RhythmActionMaskUtility
    {
        public static RhythmActionMask ToMask(RhythmAction action)
        {
            switch (action)
            {
                case RhythmAction.Guard:
                    return RhythmActionMask.Guard;
                case RhythmAction.LightAttack:
                    return RhythmActionMask.LightAttack;
                case RhythmAction.Dodge:
                    return RhythmActionMask.Dodge;
                case RhythmAction.HeavyAttack:
                    return RhythmActionMask.HeavyAttack;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported rhythm action.");
            }
        }

        public static bool Contains(RhythmActionMask mask, RhythmAction action)
        {
            RhythmActionMask actionMask = ToMask(action);
            return (mask & actionMask) == actionMask;
        }

        public static bool TryGetSingleAction(RhythmActionMask mask, out RhythmAction action)
        {
            switch (mask)
            {
                case RhythmActionMask.Guard:
                    action = RhythmAction.Guard;
                    return true;
                case RhythmActionMask.LightAttack:
                    action = RhythmAction.LightAttack;
                    return true;
                case RhythmActionMask.Dodge:
                    action = RhythmAction.Dodge;
                    return true;
                case RhythmActionMask.HeavyAttack:
                    action = RhythmAction.HeavyAttack;
                    return true;
                default:
                    action = default(RhythmAction);
                    return false;
            }
        }
    }
}
