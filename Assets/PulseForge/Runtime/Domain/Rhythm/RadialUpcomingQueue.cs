using System;
using System.Collections.Generic;

namespace PulseForge.Domain.Rhythm
{
    public readonly struct RadialUpcomingCue
    {
        public RadialUpcomingCue(
            string eventId,
            RadialEventType eventType,
            double targetTimeSeconds,
            RadialDirection direction,
            RhythmActionMask primaryActions,
            RhythmActionMask secondaryActions,
            int remainingCount)
        {
            EventId = eventId ?? string.Empty;
            EventType = eventType;
            TargetTimeSeconds = targetTimeSeconds;
            Direction = direction;
            PrimaryActions = primaryActions;
            SecondaryActions = secondaryActions;
            RemainingCount = remainingCount;
        }

        public string EventId { get; }
        public RadialEventType EventType { get; }
        public double TargetTimeSeconds { get; }
        public RadialDirection Direction { get; }
        public RhythmActionMask PrimaryActions { get; }
        public RhythmActionMask SecondaryActions { get; }
        public int RemainingCount { get; }
    }

    public static class RadialUpcomingQueueBuilder
    {
        public static void Fill(
            IReadOnlyList<RadialEncounterRuntime> encounters,
            int maximumCount,
            List<RadialUpcomingCue> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.Clear();
            if (encounters == null || maximumCount <= 0)
            {
                return;
            }

            for (int encounterIndex = 0; encounterIndex < encounters.Count; encounterIndex++)
            {
                RadialEncounterRuntime encounter = encounters[encounterIndex];
                if (encounter == null || encounter.IsResolved)
                {
                    continue;
                }

                InputRequirementRuntime active = FindNextRequirement(encounter, null);
                if (active == null)
                {
                    continue;
                }

                InputRequirementRuntime next = UsesOrderedSteps(encounter.Data.eventType)
                    ? FindNextRequirement(encounter, active)
                    : null;
                RhythmActionMask primary = encounter.Data.eventType == RadialEventType.Chord
                    ? CombineUnresolvedActions(encounter)
                    : active.Data.acceptedActions;
                int remaining = CalculateRemainingCount(encounter, active);
                RadialUpcomingCue cue = new RadialUpcomingCue(
                    encounter.Data.eventId,
                    encounter.Data.eventType,
                    active.Data.targetTimeSeconds,
                    FindDirection(encounter, active.Data.requirementId),
                    primary,
                    next == null ? RhythmActionMask.None : next.Data.acceptedActions,
                    remaining);
                InsertSorted(destination, cue, maximumCount);
            }
        }

        private static InputRequirementRuntime FindNextRequirement(
            RadialEncounterRuntime encounter,
            InputRequirementRuntime after)
        {
            InputRequirementRuntime best = null;
            for (int i = 0; i < encounter.Requirements.Count; i++)
            {
                InputRequirementRuntime candidate = encounter.Requirements[i];
                if (candidate.IsResolved || ReferenceEquals(candidate, after))
                {
                    continue;
                }
                if (after != null && candidate.Data.orderIndex <= after.Data.orderIndex)
                {
                    continue;
                }
                if (best == null
                    || candidate.Data.orderIndex < best.Data.orderIndex
                    || (candidate.Data.orderIndex == best.Data.orderIndex
                        && candidate.Data.targetTimeSeconds < best.Data.targetTimeSeconds))
                {
                    best = candidate;
                }
            }
            return best;
        }

        private static RhythmActionMask CombineUnresolvedActions(RadialEncounterRuntime encounter)
        {
            RhythmActionMask actions = RhythmActionMask.None;
            for (int i = 0; i < encounter.Requirements.Count; i++)
            {
                if (!encounter.Requirements[i].IsResolved)
                {
                    actions |= encounter.Requirements[i].Data.acceptedActions;
                }
            }
            return actions;
        }

        private static int CalculateRemainingCount(
            RadialEncounterRuntime encounter,
            InputRequirementRuntime active)
        {
            if (encounter.Data.eventType == RadialEventType.BreakTarget
                && active.Data.gestureType == InputGestureType.RepeatedPress)
            {
                return Math.Max(0, active.Data.requiredPressCount - active.AcceptedPressCount);
            }

            int remaining = 0;
            for (int i = 0; i < encounter.Requirements.Count; i++)
            {
                if (!encounter.Requirements[i].IsResolved)
                {
                    remaining++;
                }
            }
            return remaining;
        }

        private static RadialDirection FindDirection(
            RadialEncounterRuntime encounter,
            string requirementId)
        {
            for (int i = 0; i < encounter.Targets.Count; i++)
            {
                EncounterTargetData target = encounter.Targets[i].Data;
                if (string.Equals(target.requirementId, requirementId, StringComparison.Ordinal))
                {
                    return target.direction;
                }
            }
            return encounter.Targets.Count > 0
                ? encounter.Targets[0].Data.direction
                : RadialDirection.North;
        }

        private static bool UsesOrderedSteps(RadialEventType eventType)
        {
            return eventType == RadialEventType.OrderedSequence
                || eventType == RadialEventType.TimedChain
                || eventType == RadialEventType.SwarmChain
                || eventType == RadialEventType.HeavyChargeRelease;
        }

        private static void InsertSorted(
            List<RadialUpcomingCue> destination,
            RadialUpcomingCue cue,
            int maximumCount)
        {
            int insertIndex = destination.Count;
            for (int i = 0; i < destination.Count; i++)
            {
                int timeComparison = cue.TargetTimeSeconds.CompareTo(destination[i].TargetTimeSeconds);
                if (timeComparison < 0
                    || (timeComparison == 0
                        && string.CompareOrdinal(cue.EventId, destination[i].EventId) < 0))
                {
                    insertIndex = i;
                    break;
                }
            }
            if (insertIndex < maximumCount)
            {
                destination.Insert(insertIndex, cue);
                if (destination.Count > maximumCount)
                {
                    destination.RemoveAt(destination.Count - 1);
                }
            }
        }
    }
}
