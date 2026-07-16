using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PulseForge.Domain.Rhythm
{
    public sealed class RadialRhythmSession
    {
        private const double DistanceTolerance = 0.000000001d;

        private readonly List<RadialEncounterRuntime> encounters;
        private readonly ReadOnlyCollection<RadialEncounterRuntime> readOnlyEncounters;
        private readonly HashSet<RhythmAction> heldActions = new HashSet<RhythmAction>();
        private readonly HashSet<long> consumedSequenceIds = new HashSet<long>();

        public RadialRhythmSession(IEnumerable<RadialEncounterEventData> encounterData)
            : this(encounterData, TimingAssistMode.Standard)
        {
        }

        public RadialRhythmSession(
            IEnumerable<RadialEncounterEventData> encounterData,
            TimingAssistMode timingAssist)
        {
            if (encounterData == null)
            {
                throw new ArgumentNullException(nameof(encounterData));
            }

            TimingProfile = RadialTimingProfile.FromMode(timingAssist);
            encounters = new List<RadialEncounterRuntime>();
            HashSet<string> encounterIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (RadialEncounterEventData data in encounterData)
            {
                RadialEncounterRuntime runtime = new RadialEncounterRuntime(data, TimingProfile);
                if (!encounterIds.Add(runtime.Data.eventId))
                {
                    throw new ArgumentException("Encounter event ids must be unique.", nameof(encounterData));
                }

                encounters.Add(runtime);
            }

            readOnlyEncounters = new ReadOnlyCollection<RadialEncounterRuntime>(encounters);
        }

        public IReadOnlyList<RadialEncounterRuntime> Encounters => readOnlyEncounters;

        public RadialTimingProfile TimingProfile { get; }

        public int TotalEncounterCount => encounters.Count;

        public bool IsComplete
        {
            get
            {
                for (int i = 0; i < encounters.Count; i++)
                {
                    if (!encounters[i].IsResolved)
                    {
                        return false;
                    }
                }
                return encounters.Count > 0;
            }
        }

        public bool IsHeld(RhythmAction action)
        {
            return heldActions.Contains(action);
        }

        public RadialInputResolveResult Press(
            RhythmAction action,
            double songTimeSeconds,
            long sequenceId = 0L)
        {
            return ResolveInput(new RhythmInputSample(
                action,
                RhythmInputPhase.Pressed,
                songTimeSeconds,
                sequenceId));
        }

        public RadialInputResolveResult Release(
            RhythmAction action,
            double songTimeSeconds,
            long sequenceId = 0L)
        {
            return ResolveInput(new RhythmInputSample(
                action,
                RhythmInputPhase.Released,
                songTimeSeconds,
                sequenceId));
        }

        public RadialInputResolveResult ResolveInput(RhythmInputSample input)
        {
            if (input.SequenceId > 0L && !consumedSequenceIds.Add(input.SequenceId))
            {
                return new RadialInputResolveResult(false, true, null, null);
            }

            if (input.Phase == RhythmInputPhase.Pressed)
            {
                heldActions.Add(input.Action);
            }

            List<RequirementResult> requirementResults = new List<RequirementResult>();
            List<EncounterTargetResult> targetResults = new List<EncounterTargetResult>();

            RadialEncounterRuntime acceptedEncounter = FindBestEncounter(input, true);
            bool consumed = acceptedEncounter != null
                && acceptedEncounter.ResolveAcceptedInput(input, requirementResults, targetResults);
            if (!consumed)
            {
                RadialEncounterRuntime wrongInputEncounter = FindBestEncounter(input, false);
                consumed = wrongInputEncounter != null
                    && wrongInputEncounter.ResolveWrongInput(input, requirementResults, targetResults);
            }

            if (input.Phase == RhythmInputPhase.Released)
            {
                heldActions.Remove(input.Action);
            }

            return new RadialInputResolveResult(
                consumed,
                false,
                requirementResults,
                targetResults);
        }

        public IReadOnlyList<RequirementResult> Update(double songTimeSeconds)
        {
            return Update(songTimeSeconds, IsHeld);
        }

        public IReadOnlyList<RequirementResult> Update(
            double songTimeSeconds,
            Func<RhythmAction, bool> isHeld)
        {
            if (isHeld == null)
            {
                throw new ArgumentNullException(nameof(isHeld));
            }

            List<RequirementResult> requirementResults = new List<RequirementResult>();
            List<EncounterTargetResult> ignoredTargetResults = new List<EncounterTargetResult>();
            for (int i = 0; i < encounters.Count; i++)
            {
                encounters[i].Update(
                    songTimeSeconds,
                    isHeld,
                    requirementResults,
                    ignoredTargetResults);
            }

            return new ReadOnlyCollection<RequirementResult>(requirementResults);
        }

        public void Reset()
        {
            heldActions.Clear();
            consumedSequenceIds.Clear();
            for (int i = 0; i < encounters.Count; i++)
            {
                encounters[i].Reset();
            }
        }

        private RadialEncounterRuntime FindBestEncounter(RhythmInputSample input, bool acceptedInput)
        {
            RadialEncounterRuntime best = null;
            double bestDistance = double.MaxValue;
            for (int i = 0; i < encounters.Count; i++)
            {
                RadialEncounterRuntime encounter = encounters[i];
                double distance;
                bool found = acceptedInput
                    ? encounter.TryGetAcceptedInputDistance(input, out distance)
                    : encounter.TryGetWrongInputDistance(input, out distance);
                if (!found)
                {
                    continue;
                }

                if (best == null
                    || distance < bestDistance - DistanceTolerance
                    || (Math.Abs(distance - bestDistance) <= DistanceTolerance
                        && string.CompareOrdinal(encounter.Data.eventId, best.Data.eventId) < 0))
                {
                    best = encounter;
                    bestDistance = distance;
                }
            }

            return best;
        }
    }
}
