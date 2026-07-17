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
            : this(encounterData, timingAssist, 0d)
        {
        }

        public RadialRhythmSession(
            IEnumerable<RadialEncounterEventData> encounterData,
            TimingAssistMode timingAssist,
            double beatMapOffsetSeconds)
            : this(
                encounterData,
                RadialTimingProfile.FromMode(timingAssist),
                beatMapOffsetSeconds)
        {
        }

        public RadialRhythmSession(
            IEnumerable<RadialEncounterEventData> encounterData,
            RadialTimingProfile timingProfile,
            double beatMapOffsetSeconds)
        {
            if (encounterData == null)
            {
                throw new ArgumentNullException(nameof(encounterData));
            }

            ValidateFinite(beatMapOffsetSeconds, nameof(beatMapOffsetSeconds));
            TimingProfile = timingProfile;
            BeatMapOffsetSeconds = beatMapOffsetSeconds;
            encounters = new List<RadialEncounterRuntime>();
            HashSet<string> encounterIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (RadialEncounterEventData data in encounterData)
            {
                RadialEncounterEventData adjustedData = beatMapOffsetSeconds == 0d
                    ? data
                    : CloneWithOffset(data, beatMapOffsetSeconds);
                RadialEncounterRuntime runtime = new RadialEncounterRuntime(adjustedData, TimingProfile);
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

        public double BeatMapOffsetSeconds { get; }

        public RadialInputAuditRecord LastInputAudit { get; private set; }

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

        public RadialInputResolveResult Press(
            RhythmAction action,
            double rawSongTimeSeconds,
            long sequenceId,
            double inputOffsetSeconds,
            double beatMapOffsetSeconds,
            int focusedCueLimit = 1)
        {
            return ResolveInputWithTiming(
                action,
                RhythmInputPhase.Pressed,
                rawSongTimeSeconds,
                sequenceId,
                inputOffsetSeconds,
                beatMapOffsetSeconds,
                focusedCueLimit);
        }

        public RadialInputResolveResult Release(
            RhythmAction action,
            double rawSongTimeSeconds,
            long sequenceId,
            double inputOffsetSeconds,
            double beatMapOffsetSeconds,
            int focusedCueLimit = 1)
        {
            return ResolveInputWithTiming(
                action,
                RhythmInputPhase.Released,
                rawSongTimeSeconds,
                sequenceId,
                inputOffsetSeconds,
                beatMapOffsetSeconds,
                focusedCueLimit);
        }

        public RadialInputResolveResult ResolveInput(RhythmInputSample input)
        {
            return ResolveInput(
                input,
                input.SongTimeSeconds,
                0d,
                BeatMapOffsetSeconds,
                1);
        }

        public bool TryGetTimingSnapshot(
            string eventId,
            string requirementId,
            double rawSongTimeSeconds,
            double inputOffsetSeconds,
            double beatMapOffsetSeconds,
            int focusedCueLimit,
            out RadialTimingSnapshot snapshot)
        {
            if (!TryFindRequirement(
                eventId,
                requirementId,
                out RadialEncounterRuntime encounter,
                out InputRequirementRuntime requirement))
            {
                snapshot = default(RadialTimingSnapshot);
                return false;
            }
            RhythmAction action = requirement.HasCapturedInput
                ? requirement.CapturedAction
                : GetRepresentativeAction(requirement.Data.acceptedActions);
            if (TryGetInputOpportunitySnapshot(
                eventId,
                requirementId,
                action,
                encounter.GetExpectedInputPhase(requirement),
                rawSongTimeSeconds,
                inputOffsetSeconds,
                beatMapOffsetSeconds,
                focusedCueLimit,
                out InputOpportunitySnapshot opportunity))
            {
                snapshot = opportunity.Timing;
                return true;
            }
            snapshot = default(RadialTimingSnapshot);
            return false;
        }

        public bool TryGetInputOpportunitySnapshot(
            string eventId,
            string requirementId,
            RhythmAction action,
            RhythmInputPhase phase,
            double rawSongTimeSeconds,
            double inputOffsetSeconds,
            double beatMapOffsetSeconds,
            int focusedCueLimit,
            out InputOpportunitySnapshot snapshot)
        {
            ValidateFinite(rawSongTimeSeconds, nameof(rawSongTimeSeconds));
            ValidateFinite(inputOffsetSeconds, nameof(inputOffsetSeconds));
            ValidateFinite(beatMapOffsetSeconds, nameof(beatMapOffsetSeconds));
            ValidateBeatMapOffset(beatMapOffsetSeconds);
            double effectiveTime = RadialTimingMath.EffectiveJudgementTimeSeconds(
                rawSongTimeSeconds,
                inputOffsetSeconds);
            if (!TryFindRequirement(
                eventId,
                requirementId,
                out RadialEncounterRuntime encounter,
                out InputRequirementRuntime requirement))
            {
                snapshot = default(InputOpportunitySnapshot);
                return false;
            }

            RhythmInputSample input = new RhythmInputSample(action, phase, effectiveTime);
            snapshot = CreateOpportunitySnapshot(
                encounter,
                requirement,
                input,
                rawSongTimeSeconds,
                inputOffsetSeconds,
                beatMapOffsetSeconds,
                GetFocusState(encounter, effectiveTime, focusedCueLimit));
            return true;
        }

        private bool TryFindRequirement(
            string eventId,
            string requirementId,
            out RadialEncounterRuntime matchedEncounter,
            out InputRequirementRuntime matchedRequirement)
        {
            for (int encounterIndex = 0; encounterIndex < encounters.Count; encounterIndex++)
            {
                RadialEncounterRuntime encounter = encounters[encounterIndex];
                if (!string.Equals(encounter.Data.eventId, eventId, StringComparison.Ordinal))
                {
                    continue;
                }
                for (int requirementIndex = 0;
                    requirementIndex < encounter.Requirements.Count;
                    requirementIndex++)
                {
                    InputRequirementRuntime requirement = encounter.Requirements[requirementIndex];
                    if (string.Equals(
                        requirement.Data.requirementId,
                        requirementId,
                        StringComparison.Ordinal))
                    {
                        matchedEncounter = encounter;
                        matchedRequirement = requirement;
                        return true;
                    }
                }
            }

            matchedEncounter = null;
            matchedRequirement = null;
            return false;
        }

        private RadialInputResolveResult ResolveInputWithTiming(
            RhythmAction action,
            RhythmInputPhase phase,
            double rawSongTimeSeconds,
            long sequenceId,
            double inputOffsetSeconds,
            double beatMapOffsetSeconds,
            int focusedCueLimit)
        {
            ValidateFinite(rawSongTimeSeconds, nameof(rawSongTimeSeconds));
            ValidateFinite(inputOffsetSeconds, nameof(inputOffsetSeconds));
            ValidateFinite(beatMapOffsetSeconds, nameof(beatMapOffsetSeconds));
            ValidateBeatMapOffset(beatMapOffsetSeconds);
            double effectiveTime = RadialTimingMath.EffectiveJudgementTimeSeconds(
                rawSongTimeSeconds,
                inputOffsetSeconds);
            return ResolveInput(
                new RhythmInputSample(action, phase, effectiveTime, sequenceId),
                rawSongTimeSeconds,
                inputOffsetSeconds,
                beatMapOffsetSeconds,
                focusedCueLimit);
        }

        private RadialInputResolveResult ResolveInput(
            RhythmInputSample input,
            double rawSongTimeSeconds,
            double inputOffsetSeconds,
            double beatMapOffsetSeconds,
            int focusedCueLimit)
        {
            if (input.SequenceId > 0L && !consumedSequenceIds.Add(input.SequenceId))
            {
                LastInputAudit = CreateRejectedAudit(
                    input,
                    rawSongTimeSeconds,
                    inputOffsetSeconds,
                    beatMapOffsetSeconds,
                    focusedCueLimit,
                    RadialInputAuditReason.DuplicateInput);
                return new RadialInputResolveResult(false, true, null, null);
            }

            if (input.Phase == RhythmInputPhase.Pressed)
            {
                heldActions.Add(input.Action);
            }

            List<RequirementResult> requirementResults = new List<RequirementResult>();
            List<EncounterTargetResult> targetResults = new List<EncounterTargetResult>();

            InputCandidate accepted = FindBestCandidate(input, true);
            GetPrimaryFocusedIds(
                input.SongTimeSeconds,
                out string focusedEventIdBeforeResolution,
                out string focusedRequirementIdBeforeResolution);
            RadialTimingFocusState matchedFocusBeforeResolution = accepted.Encounter == null
                ? RadialTimingFocusState.None
                : GetFocusState(
                    accepted.Encounter,
                    input.SongTimeSeconds,
                    focusedCueLimit);
            int acceptedPressCount = accepted.Requirement == null
                ? 0
                : accepted.Requirement.AcceptedPressCount;
            bool consumed = accepted.Encounter != null
                && accepted.Encounter.ResolveAcceptedInput(
                    accepted.Requirement,
                    input,
                    requirementResults,
                    targetResults);
            InputCandidate matched = accepted;
            bool repeatedPressIgnored = consumed
                && accepted.Requirement != null
                && accepted.Requirement.Data.gestureType == InputGestureType.RepeatedPress
                && accepted.Requirement.AcceptedPressCount == acceptedPressCount;
            if (!consumed)
            {
                InputCandidate wrong = FindBestCandidate(input, false);
                matchedFocusBeforeResolution = wrong.Encounter == null
                    ? RadialTimingFocusState.None
                    : GetFocusState(
                        wrong.Encounter,
                        input.SongTimeSeconds,
                        focusedCueLimit);
                consumed = wrong.Encounter != null
                    && wrong.Encounter.ResolveWrongInput(
                        wrong.Requirement,
                        input,
                        requirementResults,
                        targetResults);
                matched = wrong;
            }

            if (input.Phase == RhythmInputPhase.Released)
            {
                heldActions.Remove(input.Action);
            }

            LastInputAudit = CreateAudit(
                input,
                rawSongTimeSeconds,
                inputOffsetSeconds,
                beatMapOffsetSeconds,
                focusedCueLimit,
                matched,
                consumed,
                repeatedPressIgnored,
                matchedFocusBeforeResolution,
                focusedEventIdBeforeResolution,
                focusedRequirementIdBeforeResolution);

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
            LastInputAudit = default(RadialInputAuditRecord);
            for (int i = 0; i < encounters.Count; i++)
            {
                encounters[i].Reset();
            }
        }

        private InputCandidate FindBestCandidate(RhythmInputSample input, bool acceptedInput)
        {
            InputCandidate best = default(InputCandidate);
            double bestDistance = double.MaxValue;
            for (int i = 0; i < encounters.Count; i++)
            {
                RadialEncounterRuntime encounter = encounters[i];
                double distance;
                InputRequirementRuntime requirement;
                bool found = acceptedInput
                    ? encounter.TryGetAcceptedInputCandidate(input, out requirement, out distance)
                    : encounter.TryGetWrongInputCandidate(input, out requirement, out distance);
                if (!found)
                {
                    continue;
                }

                if (best.Encounter == null
                    || distance < bestDistance - DistanceTolerance
                    || (Math.Abs(distance - bestDistance) <= DistanceTolerance
                        && IsCandidateBefore(encounter, requirement, best)))
                {
                    best = new InputCandidate(encounter, requirement, distance);
                    bestDistance = distance;
                }
            }

            return best;
        }

        private RadialInputAuditRecord CreateAudit(
            RhythmInputSample input,
            double rawSongTimeSeconds,
            double inputOffsetSeconds,
            double beatMapOffsetSeconds,
            int focusedCueLimit,
            InputCandidate matched,
            bool consumed,
            bool repeatedPressIgnored,
            RadialTimingFocusState focusBeforeResolution,
            string focusedEventIdBeforeResolution,
            string focusedRequirementIdBeforeResolution)
        {
            if (matched.Encounter == null || matched.Requirement == null)
            {
                return CreateRejectedAudit(
                    input,
                    rawSongTimeSeconds,
                    inputOffsetSeconds,
                    beatMapOffsetSeconds,
                    focusedCueLimit,
                    RadialInputAuditReason.NoActiveRequirement);
            }

            RadialTimingSnapshot snapshot = CreateSnapshot(
                matched.Encounter,
                matched.Requirement,
                rawSongTimeSeconds,
                input.SongTimeSeconds,
                inputOffsetSeconds,
                beatMapOffsetSeconds,
                focusBeforeResolution,
                input.Action,
                input.Phase);

            RadialInputAuditReason reason;
            if (repeatedPressIgnored)
            {
                reason = RadialInputAuditReason.DuplicateInput;
            }
            else if (!consumed)
            {
                reason = RadialInputAuditReason.NoActiveRequirement;
            }
            else if (!RhythmActionMaskUtility.Contains(
                matched.Requirement.Data.acceptedActions,
                input.Action))
            {
                reason = RadialInputAuditReason.WrongAction;
            }
            else if (matched.Requirement.Result != null
                && matched.Requirement.Result.Grade == HitGrade.Miss)
            {
                reason = matched.Requirement.Result.Reason == RadialResultReason.WrongInput
                    ? RadialInputAuditReason.WrongAction
                    : RadialInputAuditReason.OutsideWindow;
            }
            else if (focusBeforeResolution != RadialTimingFocusState.Focused
                && !string.IsNullOrEmpty(focusedEventIdBeforeResolution))
            {
                reason = RadialInputAuditReason.MatchedDifferentRequirement;
            }
            else
            {
                bool perfect = snapshot.UsesDeadlineWindow
                    ? snapshot.EffectiveJudgementTimeSeconds
                        <= snapshot.PerfectDeadlineSeconds + DistanceTolerance
                    : Math.Abs(snapshot.DeltaMilliseconds)
                        <= snapshot.PerfectWindowSeconds * 1000d + 0.000001d;
                reason = perfect
                        ? RadialInputAuditReason.AcceptedPerfect
                        : RadialInputAuditReason.AcceptedGood;
            }

            return new RadialInputAuditRecord(
                snapshot,
                reason,
                focusedEventIdBeforeResolution,
                focusedRequirementIdBeforeResolution);
        }

        private RadialInputAuditRecord CreateRejectedAudit(
            RhythmInputSample input,
            double rawSongTimeSeconds,
            double inputOffsetSeconds,
            double beatMapOffsetSeconds,
            int focusedCueLimit,
            RadialInputAuditReason reason)
        {
            InputOpportunityDiagnostics diagnostics = BuildOpportunityDiagnostics(
                input,
                rawSongTimeSeconds,
                inputOffsetSeconds,
                beatMapOffsetSeconds,
                focusedCueLimit,
                out InputOpportunitySnapshot nearestOpportunity);
            RadialTimingSnapshot snapshot = nearestOpportunity.EventId == null
                || nearestOpportunity.EventId.Length == 0
                    ? new RadialTimingSnapshot(
                        string.Empty,
                        string.Empty,
                        input.Action,
                        input.Phase,
                        rawSongTimeSeconds,
                        input.SongTimeSeconds,
                        input.SongTimeSeconds,
                        TimingProfile,
                        inputOffsetSeconds,
                        beatMapOffsetSeconds,
                        RadialRequirementState.Pending,
                        RadialTimingFocusState.None)
                    : nearestOpportunity.Timing;

            GetPrimaryFocusedCandidate(input.SongTimeSeconds, out InputCandidate focused);
            return new RadialInputAuditRecord(
                snapshot,
                reason,
                focused.Encounter == null ? string.Empty : focused.Encounter.Data.eventId,
                focused.Requirement == null
                    ? string.Empty
                    : focused.Requirement.Data.requirementId,
                diagnostics);
        }

        private InputOpportunityDiagnostics BuildOpportunityDiagnostics(
            RhythmInputSample input,
            double rawSongTimeSeconds,
            double inputOffsetSeconds,
            double beatMapOffsetSeconds,
            int focusedCueLimit,
            out InputOpportunitySnapshot nearestOpportunity)
        {
            int pendingCount = 0;
            int windowCandidateCount = 0;
            bool hasNearest = false;
            double nearestDistance = double.MaxValue;
            nearestOpportunity = default(InputOpportunitySnapshot);
            for (int encounterIndex = 0; encounterIndex < encounters.Count; encounterIndex++)
            {
                RadialEncounterRuntime encounter = encounters[encounterIndex];
                for (int requirementIndex = 0;
                    requirementIndex < encounter.Requirements.Count;
                    requirementIndex++)
                {
                    InputRequirementRuntime requirement = encounter.Requirements[requirementIndex];
                    if (!requirement.IsResolved)
                    {
                        pendingCount++;
                        if (encounter.IsActiveRequirement(requirement)
                            && encounter.IsTemporalWindowActive(
                                requirement,
                                input.SongTimeSeconds))
                        {
                            windowCandidateCount++;
                        }
                    }

                    InputOpportunitySnapshot opportunity = CreateOpportunitySnapshot(
                        encounter,
                        requirement,
                        input,
                        rawSongTimeSeconds,
                        inputOffsetSeconds,
                        beatMapOffsetSeconds,
                        GetFocusState(encounter, input.SongTimeSeconds, focusedCueLimit));
                    double distance = Math.Abs(opportunity.DeltaMilliseconds);
                    if (!hasNearest
                        || distance < nearestDistance - DistanceTolerance
                        || (Math.Abs(distance - nearestDistance) <= DistanceTolerance
                            && IsOpportunityBefore(opportunity, nearestOpportunity)))
                    {
                        hasNearest = true;
                        nearestDistance = distance;
                        nearestOpportunity = opportunity;
                    }
                }
            }

            return new InputOpportunityDiagnostics(
                pendingCount,
                windowCandidateCount,
                hasNearest ? nearestOpportunity.RequirementId : string.Empty,
                hasNearest
                    ? nearestOpportunity.RequirementState
                    : RadialRequirementState.Pending,
                hasNearest ? nearestOpportunity.DeltaMilliseconds : 0d,
                hasNearest
                    ? nearestOpportunity.RejectionReason
                    : InputOpportunityRejectionReason.OutsideWindow);
        }

        private static bool IsOpportunityBefore(
            InputOpportunitySnapshot left,
            InputOpportunitySnapshot right)
        {
            int eventComparison = string.CompareOrdinal(left.EventId, right.EventId);
            return eventComparison < 0
                || (eventComparison == 0
                    && string.CompareOrdinal(left.RequirementId, right.RequirementId) < 0);
        }

        private RadialTimingSnapshot CreateSnapshot(
            RadialEncounterRuntime encounter,
            InputRequirementRuntime requirement,
            double rawSongTimeSeconds,
            double effectiveTimeSeconds,
            double inputOffsetSeconds,
            double beatMapOffsetSeconds,
            RadialTimingFocusState focusState,
            RhythmAction? actionOverride = null,
            RhythmInputPhase? phaseOverride = null)
        {
            InputRequirementData data = requirement.Data;
            RhythmInputPhase expectedPhase = phaseOverride
                ?? GetExpectedPhase(encounter, requirement);
            double targetTime = encounter.Data.eventType == RadialEventType.GuardHold
                && requirement.HasCapturedInput
                    ? data.holdEndTimeSeconds
                    : data.targetTimeSeconds;
            RadialRequirementState state = requirement.IsResolved
                ? RadialRequirementState.Resolved
                : requirement.HasCapturedInput
                    ? RadialRequirementState.Captured
                    : RadialRequirementState.Pending;
            RhythmAction action = actionOverride
                ?? (requirement.HasCapturedInput
                    ? requirement.CapturedAction
                    : GetRepresentativeAction(data.acceptedActions));
            bool usesDeadline = encounter.Data.eventType == RadialEventType.BreakTarget
                && data.gestureType == InputGestureType.RepeatedPress;
            return new RadialTimingSnapshot(
                encounter.Data.eventId,
                data.requirementId,
                action,
                expectedPhase,
                rawSongTimeSeconds,
                effectiveTimeSeconds,
                targetTime,
                TimingProfile,
                inputOffsetSeconds,
                beatMapOffsetSeconds,
                state,
                focusState,
                usesDeadline,
                data.windowStartTimeSeconds,
                data.perfectDeadlineSeconds,
                data.goodDeadlineSeconds);
        }

        private InputOpportunitySnapshot CreateOpportunitySnapshot(
            RadialEncounterRuntime encounter,
            InputRequirementRuntime requirement,
            RhythmInputSample input,
            double rawSongTimeSeconds,
            double inputOffsetSeconds,
            double beatMapOffsetSeconds,
            RadialTimingFocusState focusState)
        {
            RadialTimingSnapshot timing = CreateSnapshot(
                encounter,
                requirement,
                rawSongTimeSeconds,
                input.SongTimeSeconds,
                inputOffsetSeconds,
                beatMapOffsetSeconds,
                focusState,
                input.Action,
                input.Phase);
            InputOpportunityRejectionReason rejection =
                encounter.EvaluateInputOpportunity(requirement, input);
            return new InputOpportunitySnapshot(
                timing,
                encounter.CurrentSequenceStep,
                rejection == InputOpportunityRejectionReason.None,
                rejection);
        }

        private RadialTimingFocusState GetFocusState(
            RadialEncounterRuntime encounter,
            double effectiveTimeSeconds,
            int focusedCueLimit)
        {
            if (encounter == null || encounter.IsResolved)
            {
                return RadialTimingFocusState.None;
            }

            List<InputCandidate> candidates = BuildFocusCandidates(effectiveTimeSeconds);
            int limit = Math.Max(1, Math.Min(focusedCueLimit, candidates.Count));
            for (int i = 0; i < candidates.Count; i++)
            {
                if (ReferenceEquals(candidates[i].Encounter, encounter))
                {
                    return i < limit
                        ? RadialTimingFocusState.Focused
                        : RadialTimingFocusState.Upcoming;
                }
            }
            return RadialTimingFocusState.None;
        }

        private void GetPrimaryFocusedIds(
            double effectiveTimeSeconds,
            out string eventId,
            out string requirementId)
        {
            GetPrimaryFocusedCandidate(effectiveTimeSeconds, out InputCandidate candidate);
            eventId = candidate.Encounter == null
                ? string.Empty
                : candidate.Encounter.Data.eventId;
            requirementId = candidate.Requirement == null
                ? string.Empty
                : candidate.Requirement.Data.requirementId;
        }

        private void GetPrimaryFocusedCandidate(
            double effectiveTimeSeconds,
            out InputCandidate candidate)
        {
            List<InputCandidate> candidates = BuildFocusCandidates(effectiveTimeSeconds);
            candidate = candidates.Count == 0
                ? default(InputCandidate)
                : candidates[0];
        }

        private List<InputCandidate> BuildFocusCandidates(double effectiveTimeSeconds)
        {
            List<InputCandidate> candidates = new List<InputCandidate>();
            for (int encounterIndex = 0; encounterIndex < encounters.Count; encounterIndex++)
            {
                RadialEncounterRuntime encounter = encounters[encounterIndex];
                if (encounter.IsResolved)
                {
                    continue;
                }

                InputRequirementRuntime bestRequirement = null;
                double bestTarget = double.MaxValue;
                for (int requirementIndex = 0;
                    requirementIndex < encounter.Requirements.Count;
                    requirementIndex++)
                {
                    InputRequirementRuntime requirement = encounter.Requirements[requirementIndex];
                    if (!encounter.IsActiveRequirement(requirement))
                    {
                        continue;
                    }
                    RhythmAction action = requirement.HasCapturedInput
                        ? requirement.CapturedAction
                        : GetRepresentativeAction(requirement.Data.acceptedActions);
                    RhythmInputSample focusInput = new RhythmInputSample(
                        action,
                        encounter.GetExpectedInputPhase(requirement),
                        effectiveTimeSeconds);
                    if (encounter.EvaluateInputOpportunity(requirement, focusInput)
                        != InputOpportunityRejectionReason.None)
                    {
                        continue;
                    }
                    double target = GetSnapshotTargetTime(encounter, requirement);
                    if (bestRequirement == null
                        || target < bestTarget - DistanceTolerance
                        || (Math.Abs(target - bestTarget) <= DistanceTolerance
                            && string.CompareOrdinal(
                                requirement.Data.requirementId,
                                bestRequirement.Data.requirementId) < 0))
                    {
                        bestRequirement = requirement;
                        bestTarget = target;
                    }
                }
                if (bestRequirement != null)
                {
                    InsertFocusCandidate(
                        candidates,
                        new InputCandidate(
                            encounter,
                            bestRequirement,
                            Math.Abs(effectiveTimeSeconds - bestTarget)));
                }
            }
            return candidates;
        }

        private static void InsertFocusCandidate(
            List<InputCandidate> candidates,
            InputCandidate candidate)
        {
            int insertIndex = candidates.Count;
            for (int i = 0; i < candidates.Count; i++)
            {
                InputCandidate existing = candidates[i];
                int distanceComparison = candidate.Distance.CompareTo(existing.Distance);
                if (distanceComparison < 0
                    || (distanceComparison == 0
                        && IsCandidateBefore(
                            candidate.Encounter,
                            candidate.Requirement,
                            existing)))
                {
                    insertIndex = i;
                    break;
                }
            }
            candidates.Insert(insertIndex, candidate);
        }

        private static double GetSnapshotTargetTime(
            RadialEncounterRuntime encounter,
            InputRequirementRuntime requirement)
        {
            return encounter.Data.eventType == RadialEventType.GuardHold
                && requirement.HasCapturedInput
                    ? requirement.Data.holdEndTimeSeconds
                    : requirement.Data.targetTimeSeconds;
        }

        private static RhythmInputPhase GetExpectedPhase(
            RadialEncounterRuntime encounter,
            InputRequirementRuntime requirement)
        {
            return encounter.Data.eventType == RadialEventType.GuardHold
                && requirement.HasCapturedInput
                    ? RhythmInputPhase.Released
                    : requirement.Data.phase;
        }

        private static RhythmAction GetRepresentativeAction(RhythmActionMask mask)
        {
            if (RhythmActionMaskUtility.TryGetSingleAction(mask, out RhythmAction action))
            {
                return action;
            }
            if ((mask & RhythmActionMask.Guard) != 0) return RhythmAction.Guard;
            if ((mask & RhythmActionMask.LightAttack) != 0) return RhythmAction.LightAttack;
            if ((mask & RhythmActionMask.Dodge) != 0) return RhythmAction.Dodge;
            return RhythmAction.HeavyAttack;
        }

        private static bool IsCandidateBefore(
            RadialEncounterRuntime encounter,
            InputRequirementRuntime requirement,
            InputCandidate current)
        {
            int eventComparison = string.CompareOrdinal(
                encounter.Data.eventId,
                current.Encounter.Data.eventId);
            return eventComparison < 0
                || (eventComparison == 0
                    && string.CompareOrdinal(
                        requirement.Data.requirementId,
                        current.Requirement.Data.requirementId) < 0);
        }

        private static void ValidateFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private void ValidateBeatMapOffset(double beatMapOffsetSeconds)
        {
            if (Math.Abs(beatMapOffsetSeconds - BeatMapOffsetSeconds) > DistanceTolerance)
            {
                throw new ArgumentException(
                    "Beatmap offset must match the offset applied when the session was created.",
                    nameof(beatMapOffsetSeconds));
            }
        }

        private static RadialEncounterEventData CloneWithOffset(
            RadialEncounterEventData source,
            double offsetSeconds)
        {
            RadialEncounterEventData clone = new RadialEncounterEventData
            {
                eventId = source.eventId,
                eventType = source.eventType,
                intensity = source.intensity,
                telegraphLeadSeconds = source.telegraphLeadSeconds,
                perfectSpreadSeconds = source.perfectSpreadSeconds,
                goodSpreadSeconds = source.goodSpreadSeconds,
                failureEffect = source.failureEffect == null
                    ? new FailureEffectData()
                    : new FailureEffectData
                    {
                        effectType = source.failureEffect.effectType,
                        durationSeconds = source.failureEffect.durationSeconds,
                        revealLeadMultiplier = source.failureEffect.revealLeadMultiplier,
                        minimumVisibleLeadSeconds = source.failureEffect.minimumVisibleLeadSeconds
                    }
            };
            for (int i = 0; i < source.requirements.Count; i++)
            {
                InputRequirementData data = source.requirements[i];
                clone.requirements.Add(new InputRequirementData
                {
                    requirementId = data.requirementId,
                    acceptedActions = data.acceptedActions,
                    gestureType = data.gestureType,
                    phase = data.phase,
                    targetTimeSeconds = Math.Max(0d, data.targetTimeSeconds + offsetSeconds),
                    perfectWindowSeconds = data.perfectWindowSeconds,
                    goodWindowSeconds = data.goodWindowSeconds,
                    orderIndex = data.orderIndex,
                    isOptional = data.isOptional,
                    exclusive = data.exclusive,
                    holdEndTimeSeconds = ShiftIfSet(data.holdEndTimeSeconds, offsetSeconds),
                    earlyReleaseGraceSeconds = data.earlyReleaseGraceSeconds,
                    allowEarlyReleaseAsGood = data.allowEarlyReleaseAsGood,
                    autoCompleteAtHoldEnd = data.autoCompleteAtHoldEnd,
                    pairedRequirementId = data.pairedRequirementId,
                    minimumHoldSeconds = data.minimumHoldSeconds,
                    maximumHoldSeconds = data.maximumHoldSeconds,
                    windowStartTimeSeconds = ShiftIfSet(data.windowStartTimeSeconds, offsetSeconds),
                    perfectDeadlineSeconds = ShiftIfSet(data.perfectDeadlineSeconds, offsetSeconds),
                    goodDeadlineSeconds = ShiftIfSet(data.goodDeadlineSeconds, offsetSeconds),
                    requiredPressCount = data.requiredPressCount,
                    minimumPressIntervalSeconds = data.minimumPressIntervalSeconds
                });
            }
            for (int i = 0; i < source.targets.Count; i++)
            {
                EncounterTargetData target = source.targets[i];
                clone.targets.Add(new EncounterTargetData
                {
                    targetId = target.targetId,
                    requirementId = target.requirementId,
                    direction = target.direction,
                    archetype = target.archetype
                });
            }
            return clone;
        }

        private static double ShiftIfSet(double value, double offsetSeconds)
        {
            return value == 0d ? 0d : Math.Max(0d, value + offsetSeconds);
        }

        private readonly struct InputCandidate
        {
            public InputCandidate(
                RadialEncounterRuntime encounter,
                InputRequirementRuntime requirement,
                double distance)
            {
                Encounter = encounter;
                Requirement = requirement;
                Distance = distance;
            }

            public RadialEncounterRuntime Encounter { get; }
            public InputRequirementRuntime Requirement { get; }
            public double Distance { get; }
        }
    }
}
