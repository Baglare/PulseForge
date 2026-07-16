using System;
using System.Collections.Generic;
using PulseForge.Domain.Rhythm;

namespace PulseForge.Runtime.Unity.UI
{
    public enum RadialCompoundLinkState
    {
        Pending,
        Partial,
        Complete,
        Failed
    }

    public enum RadialCompoundTargetKind
    {
        Tap,
        GuardHold,
        HeavyCharge,
        Chord,
        Choice,
        Sequence,
        TimedChain,
        Swarm,
        BreakTarget,
        Sweep
    }

    public readonly struct RadialCompoundTargetState
    {
        public RadialCompoundTargetState(
            RadialCompoundTargetKind kind,
            float progress,
            bool pressed,
            bool held,
            bool released,
            bool activeStep,
            bool completedStep,
            bool failed,
            bool earlyFailure,
            bool lateFailure,
            int stepIndex,
            int stepCount,
            int repeatCount,
            int requiredRepeatCount,
            bool hasHeavyFinisher,
            RhythmAction? selectedAction,
            bool showIndividualAction)
        {
            Kind = kind;
            Progress = progress;
            Pressed = pressed;
            Held = held;
            Released = released;
            ActiveStep = activeStep;
            CompletedStep = completedStep;
            Failed = failed;
            EarlyFailure = earlyFailure;
            LateFailure = lateFailure;
            StepIndex = stepIndex;
            StepCount = stepCount;
            RepeatCount = repeatCount;
            RequiredRepeatCount = requiredRepeatCount;
            HasHeavyFinisher = hasHeavyFinisher;
            SelectedAction = selectedAction;
            ShowIndividualAction = showIndividualAction;
        }

        public RadialCompoundTargetKind Kind { get; }
        public float Progress { get; }
        public bool Pressed { get; }
        public bool Held { get; }
        public bool Released { get; }
        public bool ActiveStep { get; }
        public bool CompletedStep { get; }
        public bool Failed { get; }
        public bool EarlyFailure { get; }
        public bool LateFailure { get; }
        public int StepIndex { get; }
        public int StepCount { get; }
        public int RepeatCount { get; }
        public int RequiredRepeatCount { get; }
        public bool HasHeavyFinisher { get; }
        public RhythmAction? SelectedAction { get; }
        public bool ShowIndividualAction { get; }
    }

    public static class RadialCompoundPresentationMath
    {
        public static float EvaluateHoldProgress(
            double songTimeSeconds,
            double pressTargetTimeSeconds,
            double holdEndTimeSeconds,
            bool hasPressed)
        {
            if (!hasPressed)
            {
                return 0f;
            }
            return RadialPresentationMath.EvaluateProgress(
                songTimeSeconds,
                pressTargetTimeSeconds,
                holdEndTimeSeconds);
        }

        public static float EvaluateChargeProgress(
            double songTimeSeconds,
            double pressTargetTimeSeconds,
            double releaseTargetTimeSeconds,
            bool hasPressed)
        {
            if (!hasPressed)
            {
                return 0f;
            }
            return RadialPresentationMath.EvaluateProgress(
                songTimeSeconds,
                pressTargetTimeSeconds,
                releaseTargetTimeSeconds);
        }

        public static RadialCompoundLinkState EvaluateChordState(
            bool firstCaptured,
            bool secondCaptured,
            bool failed)
        {
            if (failed)
            {
                return RadialCompoundLinkState.Failed;
            }
            if (firstCaptured && secondCaptured)
            {
                return RadialCompoundLinkState.Complete;
            }
            return firstCaptured || secondCaptured
                ? RadialCompoundLinkState.Partial
                : RadialCompoundLinkState.Pending;
        }

        public static RhythmAction? GetSelectedChoiceAction(
            InputRequirementRuntime requirement)
        {
            if (requirement == null)
            {
                return null;
            }
            if (requirement.Result != null)
            {
                return requirement.Result.Reason == RadialResultReason.WrongInput
                    ? (RhythmAction?)null
                    : requirement.Result.Action;
            }
            return requirement.HasCapturedInput
                ? requirement.CapturedAction
                : (RhythmAction?)null;
        }

        public static int FindActiveStepIndex(
            IReadOnlyList<InputRequirementRuntime> requirements)
        {
            if (requirements == null)
            {
                return -1;
            }
            int activeListIndex = -1;
            int activeOrder = int.MaxValue;
            for (int i = 0; i < requirements.Count; i++)
            {
                InputRequirementRuntime requirement = requirements[i];
                if (requirement == null || requirement.IsResolved)
                {
                    continue;
                }
                if (requirement.Data.orderIndex < activeOrder)
                {
                    activeOrder = requirement.Data.orderIndex;
                    activeListIndex = i;
                }
            }
            return activeListIndex;
        }

        public static int CountRemainingTargets(
            IReadOnlyList<EncounterTargetRuntime> targets)
        {
            if (targets == null)
            {
                return 0;
            }
            int remaining = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] != null && !targets[i].IsResolved)
                {
                    remaining++;
                }
            }
            return remaining;
        }

        public static int RemainingBreakSegments(int requiredCount, int acceptedCount)
        {
            return Math.Max(0, requiredCount - Math.Max(0, acceptedCount));
        }

        public static int CountDistinctRequirementGroups(
            IReadOnlyList<EncounterTargetRuntime> targets)
        {
            if (targets == null)
            {
                return 0;
            }
            int distinct = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                EncounterTargetRuntime current = targets[i];
                if (current == null)
                {
                    continue;
                }
                bool seen = false;
                for (int previous = 0; previous < i; previous++)
                {
                    EncounterTargetRuntime candidate = targets[previous];
                    if (candidate != null
                        && string.Equals(
                            candidate.Data.requirementId,
                            current.Data.requirementId,
                            StringComparison.Ordinal))
                    {
                        seen = true;
                        break;
                    }
                }
                if (!seen)
                {
                    distinct++;
                }
            }
            return distinct;
        }
    }
}
