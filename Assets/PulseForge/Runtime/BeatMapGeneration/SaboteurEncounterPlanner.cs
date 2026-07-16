using System;
using System.Collections.Generic;
using PulseForge.Domain.Rhythm;

namespace PulseForge.BeatMapGeneration
{
    internal static class SaboteurEncounterPlanner
    {
        private const double EarliestTimeSeconds = 15d;
        private const double EndingExclusionSeconds = 10d;
        private const double CompoundClearanceSeconds = 1.25d;

        public static int Apply(
            List<RadialEncounterEventData> encounters,
            double songDurationSeconds,
            BeatMapDifficulty difficulty,
            CombatStyle style,
            string deterministicSeed)
        {
            int desiredCount = DesiredCount(difficulty, style);
            if (encounters == null
                || desiredCount == 0
                || songDurationSeconds <= EarliestTimeSeconds + EndingExclusionSeconds)
            {
                return 0;
            }

            List<SaboteurCandidate> candidates = new List<SaboteurCandidate>();
            for (int i = 0; i < encounters.Count; i++)
            {
                RadialEncounterEventData encounter = encounters[i];
                if (!TryGetEligibleLightTap(encounter, out double time)
                    || time < EarliestTimeSeconds
                    || time > songDurationSeconds - EndingExclusionSeconds
                    || IsNearCompound(encounters, encounter, time))
                {
                    continue;
                }

                candidates.Add(new SaboteurCandidate(
                    encounter,
                    time,
                    RadialEncounterPlanner.StableHash(
                        (deterministicSeed ?? string.Empty) + "|saboteur|" + encounter.eventId)));
            }

            candidates.Sort((left, right) =>
            {
                int priority = right.Priority.CompareTo(left.Priority);
                return priority != 0 ? priority : left.TimeSeconds.CompareTo(right.TimeSeconds);
            });

            double minimumSpacing = MinimumSpacing(difficulty);
            List<double> selectedTimes = new List<double>();
            int converted = 0;
            for (int i = 0; i < candidates.Count && converted < desiredCount; i++)
            {
                SaboteurCandidate candidate = candidates[i];
                double duration = FogDuration(candidate.Encounter.intensity);
                if (!HasSpacing(selectedTimes, candidate.TimeSeconds, Math.Max(minimumSpacing, duration)))
                {
                    continue;
                }

                candidate.Encounter.targets[0].archetype = EnemyArchetype.Saboteur;
                candidate.Encounter.telegraphLeadSeconds = Math.Max(
                    1.05d,
                    candidate.Encounter.telegraphLeadSeconds);
                candidate.Encounter.failureEffect = new FailureEffectData
                {
                    effectType = FailureEffectType.Fog,
                    durationSeconds = duration,
                    revealLeadMultiplier = 0.55f,
                    minimumVisibleLeadSeconds = 0.45d
                };
                selectedTimes.Add(candidate.TimeSeconds);
                converted++;
            }
            return converted;
        }

        private static int DesiredCount(BeatMapDifficulty difficulty, CombatStyle style)
        {
            if (style == CombatStyle.Legacy)
            {
                return 0;
            }

            int maximum = difficulty == BeatMapDifficulty.Easy
                ? 1
                : difficulty == BeatMapDifficulty.Hard ? 3 : 2;
            switch (style)
            {
                case CombatStyle.Aggressive:
                    return maximum;
                case CombatStyle.Defensive:
                    return difficulty == BeatMapDifficulty.Easy ? 0 : 1;
                default:
                    return Math.Max(1, maximum - 1);
            }
        }

        private static double MinimumSpacing(BeatMapDifficulty difficulty)
        {
            return difficulty == BeatMapDifficulty.Easy
                ? 45d
                : difficulty == BeatMapDifficulty.Hard ? 25d : 35d;
        }

        private static double FogDuration(float intensity)
        {
            double normalized = intensity < 0f ? 0d : intensity > 1f ? 1d : intensity;
            return 5d + (4d * normalized);
        }

        private static bool TryGetEligibleLightTap(
            RadialEncounterEventData encounter,
            out double time)
        {
            time = 0d;
            if (encounter == null
                || encounter.eventType != RadialEventType.Tap
                || encounter.requirements == null
                || encounter.requirements.Count != 1
                || encounter.targets == null
                || encounter.targets.Count != 1)
            {
                return false;
            }

            InputRequirementData requirement = encounter.requirements[0];
            if (requirement == null
                || requirement.acceptedActions != RhythmActionMask.LightAttack
                || requirement.gestureType != InputGestureType.Tap
                || requirement.phase != RhythmInputPhase.Pressed)
            {
                return false;
            }

            time = requirement.targetTimeSeconds;
            return !double.IsNaN(time) && !double.IsInfinity(time);
        }

        private static bool IsNearCompound(
            List<RadialEncounterEventData> encounters,
            RadialEncounterEventData candidate,
            double candidateTime)
        {
            for (int i = 0; i < encounters.Count; i++)
            {
                RadialEncounterEventData other = encounters[i];
                if (other == null || ReferenceEquals(other, candidate) || !IsDenseCompound(other.eventType))
                {
                    continue;
                }

                GetTimeRange(other, out double start, out double end);
                if (candidateTime >= start - CompoundClearanceSeconds
                    && candidateTime <= end + CompoundClearanceSeconds)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsDenseCompound(RadialEventType type)
        {
            return type == RadialEventType.GuardHold
                || type == RadialEventType.HeavyChargeRelease
                || type == RadialEventType.Chord
                || type == RadialEventType.Choice
                || type == RadialEventType.OrderedSequence
                || type == RadialEventType.TimedChain
                || type == RadialEventType.SwarmChain
                || type == RadialEventType.BreakTarget
                || type == RadialEventType.Sweep;
        }

        private static void GetTimeRange(
            RadialEncounterEventData encounter,
            out double start,
            out double end)
        {
            start = double.MaxValue;
            end = double.MinValue;
            if (encounter.requirements != null)
            {
                for (int i = 0; i < encounter.requirements.Count; i++)
                {
                    InputRequirementData requirement = encounter.requirements[i];
                    if (requirement == null)
                    {
                        continue;
                    }
                    start = Math.Min(start, requirement.targetTimeSeconds);
                    end = Math.Max(end, Math.Max(
                        requirement.targetTimeSeconds,
                        requirement.holdEndTimeSeconds));
                }
            }
            if (start == double.MaxValue)
            {
                start = 0d;
                end = 0d;
            }
        }

        private static bool HasSpacing(
            List<double> selectedTimes,
            double candidateTime,
            double minimumSpacing)
        {
            for (int i = 0; i < selectedTimes.Count; i++)
            {
                if (Math.Abs(selectedTimes[i] - candidateTime) < minimumSpacing)
                {
                    return false;
                }
            }
            return true;
        }

        private readonly struct SaboteurCandidate
        {
            public SaboteurCandidate(
                RadialEncounterEventData encounter,
                double timeSeconds,
                uint priority)
            {
                Encounter = encounter;
                TimeSeconds = timeSeconds;
                Priority = priority;
            }

            public RadialEncounterEventData Encounter { get; }
            public double TimeSeconds { get; }
            public uint Priority { get; }
        }
    }
}
