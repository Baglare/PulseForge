using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PulseForge.Domain.Rhythm
{
    public sealed class RadialEncounterRuntime
    {
        private const double BoundaryTolerance = 0.000000001d;

        private readonly List<InputRequirementRuntime> requirements;
        private readonly ReadOnlyCollection<InputRequirementRuntime> readOnlyRequirements;
        private readonly List<EncounterTargetRuntime> targets;
        private readonly ReadOnlyCollection<EncounterTargetRuntime> readOnlyTargets;

        public RadialEncounterRuntime(RadialEncounterEventData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Validate(data);

            requirements = new List<InputRequirementRuntime>(data.requirements.Count);
            for (int i = 0; i < data.requirements.Count; i++)
            {
                requirements.Add(new InputRequirementRuntime(data.requirements[i]));
            }

            targets = new List<EncounterTargetRuntime>(data.targets.Count);
            for (int i = 0; i < data.targets.Count; i++)
            {
                targets.Add(new EncounterTargetRuntime(data.targets[i]));
            }

            readOnlyRequirements = new ReadOnlyCollection<InputRequirementRuntime>(requirements);
            readOnlyTargets = new ReadOnlyCollection<EncounterTargetRuntime>(targets);
        }

        public RadialEncounterEventData Data { get; }

        public IReadOnlyList<InputRequirementRuntime> Requirements => readOnlyRequirements;

        public IReadOnlyList<EncounterTargetRuntime> Targets => readOnlyTargets;

        public EncounterResult Result { get; private set; }

        public bool IsResolved => Result != null;

        internal bool TryGetAcceptedInputDistance(RhythmInputSample input, out double distance)
        {
            distance = double.MaxValue;
            InputRequirementRuntime requirement = FindAcceptedRequirement(input);
            if (requirement == null)
            {
                return false;
            }

            distance = GetInputDistance(requirement, input);
            return true;
        }

        internal bool TryGetWrongInputDistance(RhythmInputSample input, out double distance)
        {
            distance = double.MaxValue;
            InputRequirementRuntime requirement = FindWrongInputRequirement(input);
            if (requirement == null)
            {
                return false;
            }

            distance = GetInputDistance(requirement, input);
            return true;
        }

        internal bool ResolveAcceptedInput(
            RhythmInputSample input,
            IList<RequirementResult> newRequirementResults,
            IList<EncounterTargetResult> newTargetResults)
        {
            InputRequirementRuntime requirement = FindAcceptedRequirement(input);
            if (requirement == null)
            {
                return false;
            }

            switch (Data.eventType)
            {
                case RadialEventType.Chord:
                    ResolveChordInput(requirement, input, newRequirementResults, newTargetResults);
                    break;
                case RadialEventType.GuardHold:
                    ResolveHoldInput(requirement, input, newRequirementResults, newTargetResults);
                    break;
                case RadialEventType.HeavyChargeRelease:
                    ResolveChargeInput(requirement, input, newRequirementResults, newTargetResults);
                    break;
                case RadialEventType.BreakTarget:
                    if (requirement.Data.gestureType == InputGestureType.RepeatedPress)
                    {
                        ResolveRepeatedPress(requirement, input, newRequirementResults, newTargetResults);
                    }
                    else
                    {
                        ResolveTimedInput(requirement, input, newRequirementResults, newTargetResults);
                    }
                    break;
                default:
                    ResolveTimedInput(requirement, input, newRequirementResults, newTargetResults);
                    break;
            }

            FinalizeEncounterIfReady();
            return true;
        }

        internal bool ResolveWrongInput(
            RhythmInputSample input,
            IList<RequirementResult> newRequirementResults,
            IList<EncounterTargetResult> newTargetResults)
        {
            InputRequirementRuntime requirement = FindWrongInputRequirement(input);
            if (requirement == null)
            {
                return false;
            }

            if (Data.eventType == RadialEventType.Chord)
            {
                for (int i = 0; i < requirements.Count; i++)
                {
                    InputRequirementRuntime chordRequirement = requirements[i];
                    if (chordRequirement.IsResolved)
                    {
                        continue;
                    }

                    RadialResultReason reason = chordRequirement == requirement
                        ? RadialResultReason.WrongInput
                        : RadialResultReason.MissingChordMember;
                    ResolveRequirement(
                        chordRequirement,
                        input.Action,
                        input.Phase,
                        HitGrade.Miss,
                        reason,
                        input.SongTimeSeconds,
                        0d,
                        newRequirementResults,
                        newTargetResults);
                }
            }
            else
            {
                ResolveRequirement(
                    requirement,
                    input.Action,
                    input.Phase,
                    HitGrade.Miss,
                    RadialResultReason.WrongInput,
                    input.SongTimeSeconds,
                    0d,
                    newRequirementResults,
                    newTargetResults);
            }

            FinalizeEncounterIfReady();
            return true;
        }

        internal void Update(
            double songTimeSeconds,
            Func<RhythmAction, bool> isHeld,
            IList<RequirementResult> newRequirementResults,
            IList<EncounterTargetResult> newTargetResults)
        {
            if (double.IsNaN(songTimeSeconds) || double.IsInfinity(songTimeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(songTimeSeconds));
            }

            if (Data.eventType == RadialEventType.Chord)
            {
                UpdateChord(songTimeSeconds, newRequirementResults, newTargetResults);
                FinalizeEncounterIfReady();
                return;
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                InputRequirementRuntime requirement = requirements[i];
                if (requirement.IsResolved)
                {
                    continue;
                }

                if (Data.eventType == RadialEventType.GuardHold)
                {
                    UpdateHold(requirement, songTimeSeconds, isHeld, newRequirementResults, newTargetResults);
                }
                else if (Data.eventType == RadialEventType.BreakTarget)
                {
                    if (requirement.Data.gestureType == InputGestureType.RepeatedPress)
                    {
                        UpdateRepeatedPress(requirement, songTimeSeconds, newRequirementResults, newTargetResults);
                    }
                    else if (IsPastGoodWindow(requirement.Data, songTimeSeconds))
                    {
                        ResolveTimeout(requirement, songTimeSeconds, newRequirementResults, newTargetResults);
                    }
                }
                else if (Data.eventType == RadialEventType.HeavyChargeRelease)
                {
                    UpdateCharge(requirement, songTimeSeconds, newRequirementResults, newTargetResults);
                }
                else if (IsPastGoodWindow(requirement.Data, songTimeSeconds))
                {
                    ResolveTimeout(requirement, songTimeSeconds, newRequirementResults, newTargetResults);
                }
            }

            FinalizeEncounterIfReady();
        }

        public void Reset()
        {
            for (int i = 0; i < requirements.Count; i++)
            {
                requirements[i].Reset();
            }

            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].Reset();
            }

            Result = null;
        }

        private InputRequirementRuntime FindAcceptedRequirement(RhythmInputSample input)
        {
            List<InputRequirementRuntime> active = GetActiveRequirements();
            InputRequirementRuntime best = null;
            double bestDistance = double.MaxValue;

            for (int i = 0; i < active.Count; i++)
            {
                InputRequirementRuntime requirement = active[i];
                if (!AcceptsInput(requirement, input))
                {
                    continue;
                }

                double distance = GetInputDistance(requirement, input);
                if (best == null || distance < bestDistance)
                {
                    best = requirement;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private InputRequirementRuntime FindWrongInputRequirement(RhythmInputSample input)
        {
            List<InputRequirementRuntime> active = GetActiveRequirements();
            InputRequirementRuntime best = null;
            double bestDistance = double.MaxValue;

            for (int i = 0; i < active.Count; i++)
            {
                InputRequirementRuntime requirement = active[i];
                InputRequirementData data = requirement.Data;
                if (!data.exclusive || !IsWrongInputWindowActive(requirement, input))
                {
                    continue;
                }

                if (RhythmActionMaskUtility.Contains(data.acceptedActions, input.Action))
                {
                    continue;
                }

                double distance = GetInputDistance(requirement, input);
                if (best == null || distance < bestDistance)
                {
                    best = requirement;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private List<InputRequirementRuntime> GetActiveRequirements()
        {
            List<InputRequirementRuntime> active = new List<InputRequirementRuntime>();
            if (!UsesOrderedActivation())
            {
                for (int i = 0; i < requirements.Count; i++)
                {
                    if (!requirements[i].IsResolved)
                    {
                        active.Add(requirements[i]);
                    }
                }

                return active;
            }

            InputRequirementRuntime current = null;
            for (int i = 0; i < requirements.Count; i++)
            {
                InputRequirementRuntime candidate = requirements[i];
                if (candidate.IsResolved)
                {
                    continue;
                }

                if (current == null || candidate.Data.orderIndex < current.Data.orderIndex)
                {
                    current = candidate;
                }
            }

            if (current != null)
            {
                active.Add(current);
            }

            return active;
        }

        private bool UsesOrderedActivation()
        {
            return Data.eventType == RadialEventType.OrderedSequence
                || Data.eventType == RadialEventType.TimedChain
                || Data.eventType == RadialEventType.SwarmChain
                || Data.eventType == RadialEventType.HeavyChargeRelease;
        }

        private bool AcceptsInput(InputRequirementRuntime requirement, RhythmInputSample input)
        {
            InputRequirementData data = requirement.Data;
            if (!RhythmActionMaskUtility.Contains(data.acceptedActions, input.Action))
            {
                return false;
            }

            if (Data.eventType == RadialEventType.GuardHold && requirement.HasCapturedInput)
            {
                return input.Phase == RhythmInputPhase.Released
                    && input.Action == requirement.CapturedAction;
            }

            if (input.Phase != data.phase || requirement.HasCapturedInput)
            {
                return false;
            }

            if (Data.eventType == RadialEventType.BreakTarget
                && data.gestureType == InputGestureType.RepeatedPress)
            {
                return IsAtOrAfter(input.SongTimeSeconds, data.windowStartTimeSeconds)
                    && IsAtOrBefore(input.SongTimeSeconds, data.goodDeadlineSeconds);
            }

            if (Data.eventType == RadialEventType.HeavyChargeRelease
                && input.Phase == RhythmInputPhase.Released
                && TryGetPairedRequirement(requirement, out InputRequirementRuntime paired)
                && paired.HasCapturedInput)
            {
                return true;
            }

            return IsWithinGoodWindow(data, input.SongTimeSeconds);
        }

        private bool IsWrongInputWindowActive(InputRequirementRuntime requirement, RhythmInputSample input)
        {
            InputRequirementData data = requirement.Data;
            if (Data.eventType == RadialEventType.GuardHold && requirement.HasCapturedInput)
            {
                return input.Phase == RhythmInputPhase.Pressed
                    && IsAtOrAfter(input.SongTimeSeconds, requirement.CapturedTimeSeconds)
                    && IsAtOrBefore(input.SongTimeSeconds, data.holdEndTimeSeconds);
            }

            if (data.phase == RhythmInputPhase.Released)
            {
                if (input.Phase != RhythmInputPhase.Pressed)
                {
                    return false;
                }
            }
            else if (input.Phase != data.phase)
            {
                return false;
            }

            if (Data.eventType == RadialEventType.BreakTarget
                && data.gestureType == InputGestureType.RepeatedPress)
            {
                return IsAtOrAfter(input.SongTimeSeconds, data.windowStartTimeSeconds)
                    && IsAtOrBefore(input.SongTimeSeconds, data.goodDeadlineSeconds);
            }

            return IsWithinGoodWindow(data, input.SongTimeSeconds);
        }

        private void ResolveTimedInput(
            InputRequirementRuntime requirement,
            RhythmInputSample input,
            IList<RequirementResult> newRequirementResults,
            IList<EncounterTargetResult> newTargetResults)
        {
            HitGrade grade = GradeTiming(requirement.Data, input.SongTimeSeconds);
            RadialResultReason reason = grade == HitGrade.Miss
                ? RadialResultReason.Timing
                : RadialResultReason.None;
            requirement.Capture(input, grade);
            ResolveRequirement(
                requirement,
                input.Action,
                input.Phase,
                grade,
                reason,
                input.SongTimeSeconds,
                input.SongTimeSeconds - requirement.Data.targetTimeSeconds,
                newRequirementResults,
                newTargetResults);
        }

        private void ResolveHoldInput(
            InputRequirementRuntime requirement,
            RhythmInputSample input,
            IList<RequirementResult> newRequirementResults,
            IList<EncounterTargetResult> newTargetResults)
        {
            if (!requirement.HasCapturedInput)
            {
                HitGrade pressGrade = GradeTiming(requirement.Data, input.SongTimeSeconds);
                requirement.Capture(input, pressGrade);
                return;
            }

            InputRequirementData data = requirement.Data;
            HitGrade grade;
            RadialResultReason reason;
            if (input.SongTimeSeconds + BoundaryTolerance < data.holdEndTimeSeconds)
            {
                bool withinGoodGrace = data.allowEarlyReleaseAsGood
                    && input.SongTimeSeconds + BoundaryTolerance
                    >= data.holdEndTimeSeconds - data.earlyReleaseGraceSeconds;
                grade = withinGoodGrace
                    ? Worst(requirement.CapturedGrade, HitGrade.Good)
                    : HitGrade.Miss;
                reason = RadialResultReason.EarlyRelease;
            }
            else
            {
                grade = requirement.CapturedGrade;
                reason = RadialResultReason.None;
            }

            ResolveRequirement(
                requirement,
                input.Action,
                input.Phase,
                grade,
                reason,
                input.SongTimeSeconds,
                input.SongTimeSeconds - data.holdEndTimeSeconds,
                newRequirementResults,
                newTargetResults);
        }

        private void ResolveChargeInput(
            InputRequirementRuntime requirement,
            RhythmInputSample input,
            IList<RequirementResult> newRequirementResults,
            IList<EncounterTargetResult> newTargetResults)
        {
            HitGrade grade = GradeTiming(requirement.Data, input.SongTimeSeconds);
            RadialResultReason reason = grade == HitGrade.Miss
                ? RadialResultReason.Timing
                : RadialResultReason.None;
            requirement.Capture(input, grade);

            if (input.Phase == RhythmInputPhase.Released)
            {
                if (!TryGetPairedRequirement(requirement, out InputRequirementRuntime paired)
                    || !paired.HasCapturedInput)
                {
                    grade = HitGrade.Miss;
                    reason = RadialResultReason.InvalidCharge;
                }
                else
                {
                    double duration = input.SongTimeSeconds - paired.CapturedTimeSeconds;
                    if (duration + BoundaryTolerance < requirement.Data.minimumHoldSeconds
                        || duration - BoundaryTolerance > requirement.Data.maximumHoldSeconds)
                    {
                        grade = HitGrade.Miss;
                        reason = RadialResultReason.InvalidCharge;
                    }
                }
            }

            ResolveRequirement(
                requirement,
                input.Action,
                input.Phase,
                grade,
                reason,
                input.SongTimeSeconds,
                input.SongTimeSeconds - requirement.Data.targetTimeSeconds,
                newRequirementResults,
                newTargetResults);
        }

        private void ResolveChordInput(
            InputRequirementRuntime requirement,
            RhythmInputSample input,
            IList<RequirementResult> newRequirementResults,
            IList<EncounterTargetResult> newTargetResults)
        {
            requirement.Capture(input, GradeTiming(requirement.Data, input.SongTimeSeconds));

            for (int i = 0; i < requirements.Count; i++)
            {
                if (!requirements[i].HasCapturedInput)
                {
                    return;
                }
            }

            double earliest = double.MaxValue;
            double latest = double.MinValue;
            HitGrade grade = HitGrade.Perfect;
            for (int i = 0; i < requirements.Count; i++)
            {
                InputRequirementRuntime chordRequirement = requirements[i];
                earliest = Math.Min(earliest, chordRequirement.CapturedTimeSeconds);
                latest = Math.Max(latest, chordRequirement.CapturedTimeSeconds);
                grade = Worst(grade, chordRequirement.CapturedGrade);
            }

            double spread = latest - earliest;
            HitGrade spreadGrade = spread <= Data.perfectSpreadSeconds + BoundaryTolerance
                ? HitGrade.Perfect
                : spread <= Data.goodSpreadSeconds + BoundaryTolerance
                    ? HitGrade.Good
                    : HitGrade.Miss;
            grade = Worst(grade, spreadGrade);
            RadialResultReason reason = spreadGrade == HitGrade.Miss
                ? RadialResultReason.ChordSpread
                : RadialResultReason.None;

            for (int i = 0; i < requirements.Count; i++)
            {
                InputRequirementRuntime chordRequirement = requirements[i];
                ResolveRequirement(
                    chordRequirement,
                    chordRequirement.CapturedAction,
                    chordRequirement.CapturedPhase,
                    grade,
                    reason,
                    chordRequirement.CapturedTimeSeconds,
                    chordRequirement.CapturedTimeSeconds - chordRequirement.Data.targetTimeSeconds,
                    newRequirementResults,
                    newTargetResults);
            }
        }

        private void ResolveRepeatedPress(
            InputRequirementRuntime requirement,
            RhythmInputSample input,
            IList<RequirementResult> newRequirementResults,
            IList<EncounterTargetResult> newTargetResults)
        {
            InputRequirementData data = requirement.Data;
            if (requirement.AcceptedPressCount > 0
                && input.SongTimeSeconds - requirement.LastAcceptedPressTimeSeconds
                + BoundaryTolerance < data.minimumPressIntervalSeconds)
            {
                return;
            }

            requirement.RegisterPress(input.SongTimeSeconds);
            if (requirement.AcceptedPressCount < data.requiredPressCount)
            {
                return;
            }

            HitGrade grade = IsAtOrBefore(input.SongTimeSeconds, data.perfectDeadlineSeconds)
                ? HitGrade.Perfect
                : IsAtOrBefore(input.SongTimeSeconds, data.goodDeadlineSeconds)
                    ? HitGrade.Good
                    : HitGrade.Miss;
            ResolveRequirement(
                requirement,
                input.Action,
                input.Phase,
                grade,
                grade == HitGrade.Miss ? RadialResultReason.Timing : RadialResultReason.None,
                input.SongTimeSeconds,
                0d,
                newRequirementResults,
                newTargetResults);
        }

        private void UpdateChord(
            double songTimeSeconds,
            IList<RequirementResult> newRequirementResults,
            IList<EncounterTargetResult> newTargetResults)
        {
            double deadline = double.MinValue;
            for (int i = 0; i < requirements.Count; i++)
            {
                InputRequirementData data = requirements[i].Data;
                deadline = Math.Max(deadline, data.targetTimeSeconds + data.goodWindowSeconds);
            }

            if (songTimeSeconds <= deadline + BoundaryTolerance)
            {
                return;
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                InputRequirementRuntime requirement = requirements[i];
                if (!requirement.IsResolved)
                {
                    ResolveRequirement(
                        requirement,
                        requirement.HasCapturedInput
                            ? requirement.CapturedAction
                            : GetRepresentativeAction(requirement.Data.acceptedActions),
                        requirement.HasCapturedInput
                            ? requirement.CapturedPhase
                            : requirement.Data.phase,
                        HitGrade.Miss,
                        requirement.HasCapturedInput
                            ? RadialResultReason.MissingChordMember
                            : RadialResultReason.Timeout,
                        songTimeSeconds,
                        0d,
                        newRequirementResults,
                        newTargetResults);
                }
            }
        }

        private void UpdateHold(
            InputRequirementRuntime requirement,
            double songTimeSeconds,
            Func<RhythmAction, bool> isHeld,
            IList<RequirementResult> newRequirementResults,
            IList<EncounterTargetResult> newTargetResults)
        {
            InputRequirementData data = requirement.Data;
            if (!requirement.HasCapturedInput)
            {
                if (IsPastGoodWindow(data, songTimeSeconds))
                {
                    ResolveTimeout(requirement, songTimeSeconds, newRequirementResults, newTargetResults);
                }

                return;
            }

            if (songTimeSeconds + BoundaryTolerance < data.holdEndTimeSeconds)
            {
                return;
            }

            bool held = isHeld != null && isHeld(requirement.CapturedAction);
            if (data.autoCompleteAtHoldEnd && held)
            {
                ResolveRequirement(
                    requirement,
                    requirement.CapturedAction,
                    RhythmInputPhase.Released,
                    requirement.CapturedGrade,
                    RadialResultReason.None,
                    data.holdEndTimeSeconds,
                    0d,
                    newRequirementResults,
                    newTargetResults);
                return;
            }

            if (!held || songTimeSeconds > data.holdEndTimeSeconds + data.goodWindowSeconds + BoundaryTolerance)
            {
                ResolveRequirement(
                    requirement,
                    requirement.CapturedAction,
                    RhythmInputPhase.Released,
                    HitGrade.Miss,
                    !held ? RadialResultReason.EarlyRelease : RadialResultReason.Timeout,
                    songTimeSeconds,
                    songTimeSeconds - data.holdEndTimeSeconds,
                    newRequirementResults,
                    newTargetResults);
            }
        }

        private void UpdateCharge(
            InputRequirementRuntime requirement,
            double songTimeSeconds,
            IList<RequirementResult> newRequirementResults,
            IList<EncounterTargetResult> newTargetResults)
        {
            if (requirement.Data.phase == RhythmInputPhase.Released
                && TryGetPairedRequirement(requirement, out InputRequirementRuntime paired)
                && paired.HasCapturedInput
                && songTimeSeconds - paired.CapturedTimeSeconds
                > requirement.Data.maximumHoldSeconds + BoundaryTolerance)
            {
                ResolveRequirement(
                    requirement,
                    GetRepresentativeAction(requirement.Data.acceptedActions),
                    requirement.Data.phase,
                    HitGrade.Miss,
                    RadialResultReason.InvalidCharge,
                    songTimeSeconds,
                    0d,
                    newRequirementResults,
                    newTargetResults);
                return;
            }

            if (IsPastGoodWindow(requirement.Data, songTimeSeconds))
            {
                ResolveTimeout(requirement, songTimeSeconds, newRequirementResults, newTargetResults);
            }
        }

        private void UpdateRepeatedPress(
            InputRequirementRuntime requirement,
            double songTimeSeconds,
            IList<RequirementResult> newRequirementResults,
            IList<EncounterTargetResult> newTargetResults)
        {
            if (songTimeSeconds <= requirement.Data.goodDeadlineSeconds + BoundaryTolerance)
            {
                return;
            }

            ResolveRequirement(
                requirement,
                RhythmAction.LightAttack,
                RhythmInputPhase.Pressed,
                HitGrade.Miss,
                RadialResultReason.InsufficientCount,
                songTimeSeconds,
                0d,
                newRequirementResults,
                newTargetResults);
        }

        private void ResolveTimeout(
            InputRequirementRuntime requirement,
            double songTimeSeconds,
            IList<RequirementResult> newRequirementResults,
            IList<EncounterTargetResult> newTargetResults)
        {
            ResolveRequirement(
                requirement,
                GetRepresentativeAction(requirement.Data.acceptedActions),
                requirement.Data.phase,
                HitGrade.Miss,
                RadialResultReason.Timeout,
                songTimeSeconds,
                songTimeSeconds - requirement.Data.targetTimeSeconds,
                newRequirementResults,
                newTargetResults);
        }

        private void ResolveRequirement(
            InputRequirementRuntime requirement,
            RhythmAction action,
            RhythmInputPhase phase,
            HitGrade grade,
            RadialResultReason reason,
            double resolutionTimeSeconds,
            double timingErrorSeconds,
            IList<RequirementResult> newRequirementResults,
            IList<EncounterTargetResult> newTargetResults)
        {
            RequirementResult result = new RequirementResult(
                Data.eventId,
                requirement.Data.requirementId,
                action,
                phase,
                grade,
                reason,
                resolutionTimeSeconds,
                timingErrorSeconds);
            requirement.Resolve(result);
            newRequirementResults?.Add(result);

            for (int i = 0; i < targets.Count; i++)
            {
                EncounterTargetRuntime target = targets[i];
                if (target.IsResolved
                    || !string.Equals(
                        target.Data.requirementId,
                        requirement.Data.requirementId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                EncounterTargetResult targetResult = new EncounterTargetResult(
                    Data.eventId,
                    target.Data.targetId,
                    requirement.Data.requirementId,
                    grade,
                    reason);
                target.Resolve(targetResult);
                newTargetResults?.Add(targetResult);
            }
        }

        private void FinalizeEncounterIfReady()
        {
            if (Result != null)
            {
                return;
            }

            HitGrade aggregate = HitGrade.Perfect;
            for (int i = 0; i < requirements.Count; i++)
            {
                InputRequirementRuntime requirement = requirements[i];
                if (requirement.Data.isOptional)
                {
                    continue;
                }

                if (!requirement.IsResolved)
                {
                    return;
                }

                aggregate = Worst(aggregate, requirement.Result.Grade);
            }

            Result = new EncounterResult(Data.eventId, aggregate);
        }

        private bool TryGetPairedRequirement(
            InputRequirementRuntime requirement,
            out InputRequirementRuntime paired)
        {
            string pairedId = requirement.Data.pairedRequirementId;
            for (int i = 0; i < requirements.Count; i++)
            {
                if (string.Equals(requirements[i].Data.requirementId, pairedId, StringComparison.Ordinal))
                {
                    paired = requirements[i];
                    return true;
                }
            }

            paired = null;
            return false;
        }

        private double GetInputDistance(InputRequirementRuntime requirement, RhythmInputSample input)
        {
            if (Data.eventType == RadialEventType.GuardHold && requirement.HasCapturedInput)
            {
                return Math.Abs(input.SongTimeSeconds - requirement.Data.holdEndTimeSeconds);
            }

            if (Data.eventType == RadialEventType.BreakTarget
                && requirement.Data.gestureType == InputGestureType.RepeatedPress)
            {
                return 0d;
            }

            return Math.Abs(input.SongTimeSeconds - requirement.Data.targetTimeSeconds);
        }

        private static HitGrade GradeTiming(InputRequirementData data, double inputTimeSeconds)
        {
            double absoluteError = Math.Abs(inputTimeSeconds - data.targetTimeSeconds);
            if (absoluteError <= data.perfectWindowSeconds + BoundaryTolerance)
            {
                return HitGrade.Perfect;
            }

            return absoluteError <= data.goodWindowSeconds + BoundaryTolerance
                ? HitGrade.Good
                : HitGrade.Miss;
        }

        private static bool IsWithinGoodWindow(InputRequirementData data, double songTimeSeconds)
        {
            return Math.Abs(songTimeSeconds - data.targetTimeSeconds)
                <= data.goodWindowSeconds + BoundaryTolerance;
        }

        private static bool IsPastGoodWindow(InputRequirementData data, double songTimeSeconds)
        {
            return songTimeSeconds
                > data.targetTimeSeconds + data.goodWindowSeconds + BoundaryTolerance;
        }

        private static bool IsAtOrBefore(double value, double boundary)
        {
            return value <= boundary + BoundaryTolerance;
        }

        private static bool IsAtOrAfter(double value, double boundary)
        {
            return value + BoundaryTolerance >= boundary;
        }

        private static HitGrade Worst(HitGrade left, HitGrade right)
        {
            if (left == HitGrade.Miss || right == HitGrade.Miss)
            {
                return HitGrade.Miss;
            }

            if (left == HitGrade.Good || right == HitGrade.Good)
            {
                return HitGrade.Good;
            }

            return HitGrade.Perfect;
        }

        private static RhythmAction GetRepresentativeAction(RhythmActionMask mask)
        {
            if (RhythmActionMaskUtility.TryGetSingleAction(mask, out RhythmAction action))
            {
                return action;
            }

            if ((mask & RhythmActionMask.Guard) != 0)
            {
                return RhythmAction.Guard;
            }

            if ((mask & RhythmActionMask.LightAttack) != 0)
            {
                return RhythmAction.LightAttack;
            }

            if ((mask & RhythmActionMask.Dodge) != 0)
            {
                return RhythmAction.Dodge;
            }

            return RhythmAction.HeavyAttack;
        }

        private static void Validate(RadialEncounterEventData data)
        {
            if (string.IsNullOrWhiteSpace(data.eventId))
            {
                throw new ArgumentException("Encounter event id is required.", nameof(data));
            }

            if (data.requirements == null || data.requirements.Count == 0)
            {
                throw new ArgumentException("An encounter requires at least one input requirement.", nameof(data));
            }

            if (data.targets == null)
            {
                throw new ArgumentException("Encounter targets cannot be null.", nameof(data));
            }

            HashSet<string> requirementIds = new HashSet<string>(StringComparer.Ordinal);
            int requiredCount = 0;
            int requiredBreakCount = 0;
            for (int i = 0; i < data.requirements.Count; i++)
            {
                InputRequirementData requirement = data.requirements[i]
                    ?? throw new ArgumentException("Input requirements cannot contain null entries.", nameof(data));
                if (string.IsNullOrWhiteSpace(requirement.requirementId)
                    || !requirementIds.Add(requirement.requirementId))
                {
                    throw new ArgumentException("Input requirement ids must be non-empty and unique.", nameof(data));
                }

                if (requirement.acceptedActions == RhythmActionMask.None)
                {
                    throw new ArgumentException("Every requirement must accept at least one action.", nameof(data));
                }

                ValidateWindow(requirement);
                if (!requirement.isOptional)
                {
                    requiredCount++;
                }

                if (data.eventType == RadialEventType.GuardHold
                    && requirement.holdEndTimeSeconds + BoundaryTolerance < requirement.targetTimeSeconds)
                {
                    throw new ArgumentException("A hold cannot end before its press target.", nameof(data));
                }

                if (data.eventType == RadialEventType.HeavyChargeRelease
                    && requirement.phase == RhythmInputPhase.Released
                    && (requirement.minimumHoldSeconds < 0d
                        || requirement.maximumHoldSeconds + BoundaryTolerance < requirement.minimumHoldSeconds))
                {
                    throw new ArgumentException("Heavy charge duration limits are invalid.", nameof(data));
                }

                if (data.eventType == RadialEventType.BreakTarget
                    && requirement.gestureType == InputGestureType.RepeatedPress)
                {
                    if (requirement.acceptedActions != RhythmActionMask.LightAttack
                        || requirement.requiredPressCount <= 0
                        || requirement.minimumPressIntervalSeconds < 0d
                        || requirement.perfectDeadlineSeconds + BoundaryTolerance < requirement.windowStartTimeSeconds
                        || requirement.goodDeadlineSeconds + BoundaryTolerance < requirement.perfectDeadlineSeconds)
                    {
                        throw new ArgumentException("Break Target requires valid Light Attack count and deadlines.", nameof(data));
                    }

                    if (!requirement.isOptional)
                    {
                        requiredBreakCount++;
                    }
                }
            }

            if (requiredCount == 0)
            {
                throw new ArgumentException("An encounter needs at least one required requirement.", nameof(data));
            }

            if (data.eventType == RadialEventType.BreakTarget && requiredBreakCount == 0)
            {
                throw new ArgumentException("Break Target needs a required repeated Light Attack requirement.", nameof(data));
            }

            ValidateChord(data);

            HashSet<string> targetIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < data.targets.Count; i++)
            {
                EncounterTargetData target = data.targets[i]
                    ?? throw new ArgumentException("Encounter targets cannot contain null entries.", nameof(data));
                if (string.IsNullOrWhiteSpace(target.targetId) || !targetIds.Add(target.targetId))
                {
                    throw new ArgumentException("Encounter target ids must be non-empty and unique.", nameof(data));
                }

                if (!requirementIds.Contains(target.requirementId))
                {
                    throw new ArgumentException("Every target must reference an encounter requirement.", nameof(data));
                }
            }
        }

        private static void ValidateWindow(InputRequirementData requirement)
        {
            if (double.IsNaN(requirement.targetTimeSeconds)
                || double.IsInfinity(requirement.targetTimeSeconds)
                || requirement.perfectWindowSeconds < 0d
                || requirement.goodWindowSeconds < requirement.perfectWindowSeconds)
            {
                throw new ArgumentException("Requirement timing windows are invalid.", nameof(requirement));
            }
        }

        private static void ValidateChord(RadialEncounterEventData data)
        {
            if (data.eventType != RadialEventType.Chord)
            {
                return;
            }

            if (data.requirements.Count != 2
                || data.perfectSpreadSeconds < 0d
                || data.goodSpreadSeconds < data.perfectSpreadSeconds)
            {
                throw new ArgumentException("A chord requires two actions and valid spread windows.", nameof(data));
            }

            HashSet<RhythmAction> actions = new HashSet<RhythmAction>();
            for (int i = 0; i < data.requirements.Count; i++)
            {
                InputRequirementData requirement = data.requirements[i];
                if (requirement.isOptional
                    || !RhythmActionMaskUtility.TryGetSingleAction(requirement.acceptedActions, out RhythmAction action)
                    || !actions.Add(action))
                {
                    throw new ArgumentException("Chord actions must be distinct and required.", nameof(data));
                }
            }
        }
    }
}
