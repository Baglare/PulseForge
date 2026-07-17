using System;

namespace PulseForge.Domain.Rhythm
{
    public static class RadialTimingMath
    {
        public static double EffectiveJudgementTimeSeconds(
            double rawSongTimeSeconds,
            double inputOffsetSeconds)
        {
            if (double.IsNaN(rawSongTimeSeconds) || double.IsInfinity(rawSongTimeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(rawSongTimeSeconds));
            }
            if (double.IsNaN(inputOffsetSeconds) || double.IsInfinity(inputOffsetSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(inputOffsetSeconds));
            }
            return rawSongTimeSeconds + inputOffsetSeconds;
        }

        public static double InputOffsetForObservedDelta(
            double currentInputOffsetSeconds,
            double observedDeltaSeconds)
        {
            return currentInputOffsetSeconds - observedDeltaSeconds;
        }
    }

    public enum RadialRequirementState
    {
        Pending,
        Captured,
        Resolved
    }

    public enum RadialTimingFocusState
    {
        None,
        Focused,
        Upcoming
    }

    public enum RadialInputAuditReason
    {
        AcceptedPerfect,
        AcceptedGood,
        OutsideWindow,
        WrongAction,
        WrongPhase,
        NoActiveRequirement,
        FutureSequenceStep,
        DuplicateInput,
        AlreadyResolved,
        MatchedDifferentRequirement
    }

    public enum InputOpportunityRejectionReason
    {
        None,
        WrongAction,
        WrongPhase,
        NotCurrentStep,
        AlreadyResolved,
        TimedOut,
        OutsideWindow
    }

    public readonly struct RadialTimingSnapshot
    {
        public RadialTimingSnapshot(
            string eventId,
            string requirementId,
            RhythmAction action,
            RhythmInputPhase phase,
            double rawSongTimeSeconds,
            double effectiveJudgementTimeSeconds,
            double targetTimeSeconds,
            RadialTimingProfile timingProfile,
            double inputOffsetSeconds,
            double beatMapOffsetSeconds,
            RadialRequirementState requirementState,
            RadialTimingFocusState focusState,
            bool usesDeadlineWindow = false,
            double windowStartTimeSeconds = 0d,
            double perfectDeadlineSeconds = 0d,
            double goodDeadlineSeconds = 0d)
        {
            EventId = eventId ?? string.Empty;
            RequirementId = requirementId ?? string.Empty;
            Action = action;
            Phase = phase;
            RawSongTimeSeconds = rawSongTimeSeconds;
            EffectiveJudgementTimeSeconds = effectiveJudgementTimeSeconds;
            TargetTimeSeconds = targetTimeSeconds;
            DeltaMilliseconds = (effectiveJudgementTimeSeconds - targetTimeSeconds) * 1000d;
            PerfectWindowSeconds = timingProfile.PerfectWindowSeconds;
            GoodWindowSeconds = timingProfile.GoodWindowSeconds;
            TimingAssist = timingProfile.Mode;
            InputOffsetSeconds = inputOffsetSeconds;
            BeatMapOffsetSeconds = beatMapOffsetSeconds;
            RequirementState = requirementState;
            FocusState = focusState;
            UsesDeadlineWindow = usesDeadlineWindow;
            WindowStartTimeSeconds = windowStartTimeSeconds;
            PerfectDeadlineSeconds = perfectDeadlineSeconds;
            GoodDeadlineSeconds = goodDeadlineSeconds;
        }

        public string EventId { get; }
        public string RequirementId { get; }
        public RhythmAction Action { get; }
        public RhythmInputPhase Phase { get; }
        public double RawSongTimeSeconds { get; }
        public double EffectiveJudgementTimeSeconds { get; }
        public double TargetTimeSeconds { get; }
        public double DeltaMilliseconds { get; }
        public double PerfectWindowSeconds { get; }
        public double GoodWindowSeconds { get; }
        public TimingAssistMode TimingAssist { get; }
        public double InputOffsetSeconds { get; }
        public double BeatMapOffsetSeconds { get; }
        public RadialRequirementState RequirementState { get; }
        public RadialTimingFocusState FocusState { get; }
        public bool UsesDeadlineWindow { get; }
        public double WindowStartTimeSeconds { get; }
        public double PerfectDeadlineSeconds { get; }
        public double GoodDeadlineSeconds { get; }
    }

    public readonly struct InputOpportunitySnapshot
    {
        public InputOpportunitySnapshot(
            RadialTimingSnapshot timing,
            int currentSequenceStep,
            bool matchable,
            InputOpportunityRejectionReason rejectionReason)
        {
            Timing = timing;
            CurrentSequenceStep = currentSequenceStep;
            Matchable = matchable;
            RejectionReason = rejectionReason;
        }

        public RadialTimingSnapshot Timing { get; }
        public string EventId => Timing.EventId;
        public string RequirementId => Timing.RequirementId;
        public RhythmAction Action => Timing.Action;
        public RhythmInputPhase Phase => Timing.Phase;
        public RadialRequirementState RequirementState => Timing.RequirementState;
        public int CurrentSequenceStep { get; }
        public double RawSongTimeSeconds => Timing.RawSongTimeSeconds;
        public double EffectiveInputTimeSeconds => Timing.EffectiveJudgementTimeSeconds;
        public double TargetTimeSeconds => Timing.TargetTimeSeconds;
        public double DeltaMilliseconds => Timing.DeltaMilliseconds;
        public double PerfectWindowSeconds => Timing.PerfectWindowSeconds;
        public double GoodWindowSeconds => Timing.GoodWindowSeconds;
        public bool Matchable { get; }
        public InputOpportunityRejectionReason RejectionReason { get; }
    }

    public readonly struct InputOpportunityDiagnostics
    {
        public InputOpportunityDiagnostics(
            int pendingRequirementCount,
            int windowCandidateCount,
            string nearestRequirementId,
            RadialRequirementState nearestRequirementState,
            double nearestDeltaMilliseconds,
            InputOpportunityRejectionReason nearestRejectionReason)
        {
            PendingRequirementCount = pendingRequirementCount;
            WindowCandidateCount = windowCandidateCount;
            NearestRequirementId = nearestRequirementId ?? string.Empty;
            NearestRequirementState = nearestRequirementState;
            NearestDeltaMilliseconds = nearestDeltaMilliseconds;
            NearestRejectionReason = nearestRejectionReason;
        }

        public int PendingRequirementCount { get; }
        public int WindowCandidateCount { get; }
        public string NearestRequirementId { get; }
        public RadialRequirementState NearestRequirementState { get; }
        public double NearestDeltaMilliseconds { get; }
        public InputOpportunityRejectionReason NearestRejectionReason { get; }
    }

    public readonly struct RadialInputAuditRecord
    {
        public RadialInputAuditRecord(
            RadialTimingSnapshot timing,
            RadialInputAuditReason reason,
            string focusedEventId,
            string focusedRequirementId,
            InputOpportunityDiagnostics diagnostics = default(InputOpportunityDiagnostics))
        {
            Timing = timing;
            Reason = reason;
            FocusedEventId = focusedEventId ?? string.Empty;
            FocusedRequirementId = focusedRequirementId ?? string.Empty;
            Diagnostics = diagnostics;
        }

        public RadialTimingSnapshot Timing { get; }
        public RadialInputAuditReason Reason { get; }
        public string FocusedEventId { get; }
        public string FocusedRequirementId { get; }
        public InputOpportunityDiagnostics Diagnostics { get; }
        public bool Accepted => Reason == RadialInputAuditReason.AcceptedPerfect
            || Reason == RadialInputAuditReason.AcceptedGood
            || Reason == RadialInputAuditReason.MatchedDifferentRequirement;
    }
}
