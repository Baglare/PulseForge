using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PulseForge.Domain.Rhythm
{
    public enum RadialResultReason
    {
        None,
        Timing,
        Timeout,
        WrongInput,
        EarlyRelease,
        InvalidCharge,
        ChordSpread,
        MissingChordMember,
        InsufficientCount
    }

    public readonly struct RhythmInputSample
    {
        public RhythmInputSample(
            RhythmAction action,
            RhythmInputPhase phase,
            double songTimeSeconds,
            long sequenceId = 0L)
        {
            if (double.IsNaN(songTimeSeconds) || double.IsInfinity(songTimeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(songTimeSeconds));
            }

            Action = action;
            Phase = phase;
            SongTimeSeconds = songTimeSeconds;
            SequenceId = sequenceId;
        }

        public RhythmAction Action { get; }

        public RhythmInputPhase Phase { get; }

        public double SongTimeSeconds { get; }

        public long SequenceId { get; }
    }

    public sealed class RequirementResult
    {
        public RequirementResult(
            string encounterId,
            string requirementId,
            RhythmAction action,
            RhythmInputPhase phase,
            HitGrade grade,
            RadialResultReason reason,
            double resolutionTimeSeconds,
            double timingErrorSeconds)
        {
            EncounterId = encounterId ?? string.Empty;
            RequirementId = requirementId ?? string.Empty;
            Action = action;
            Phase = phase;
            Grade = grade;
            Reason = reason;
            ResolutionTimeSeconds = resolutionTimeSeconds;
            TimingErrorSeconds = timingErrorSeconds;
        }

        public string EncounterId { get; }

        public string RequirementId { get; }

        public RhythmAction Action { get; }

        public RhythmInputPhase Phase { get; }

        public HitGrade Grade { get; }

        public RadialResultReason Reason { get; }

        public double ResolutionTimeSeconds { get; }

        public double TimingErrorSeconds { get; }
    }

    public sealed class EncounterTargetResult
    {
        public EncounterTargetResult(
            string encounterId,
            string targetId,
            string requirementId,
            HitGrade grade,
            RadialResultReason reason)
        {
            EncounterId = encounterId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            RequirementId = requirementId ?? string.Empty;
            Grade = grade;
            Reason = reason;
        }

        public string EncounterId { get; }

        public string TargetId { get; }

        public string RequirementId { get; }

        public HitGrade Grade { get; }

        public RadialResultReason Reason { get; }
    }

    public sealed class EncounterResult
    {
        public EncounterResult(string encounterId, HitGrade grade)
        {
            EncounterId = encounterId ?? string.Empty;
            Grade = grade;
        }

        public string EncounterId { get; }

        public HitGrade Grade { get; }
    }

    public sealed class RadialInputResolveResult
    {
        private static readonly ReadOnlyCollection<RequirementResult> NoRequirementResults =
            new ReadOnlyCollection<RequirementResult>(new List<RequirementResult>());

        private static readonly ReadOnlyCollection<EncounterTargetResult> NoTargetResults =
            new ReadOnlyCollection<EncounterTargetResult>(new List<EncounterTargetResult>());

        internal RadialInputResolveResult(
            bool consumed,
            bool duplicate,
            IList<RequirementResult> requirementResults,
            IList<EncounterTargetResult> targetResults)
        {
            Consumed = consumed;
            Duplicate = duplicate;
            RequirementResults = requirementResults == null
                ? NoRequirementResults
                : new ReadOnlyCollection<RequirementResult>(new List<RequirementResult>(requirementResults));
            TargetResults = targetResults == null
                ? NoTargetResults
                : new ReadOnlyCollection<EncounterTargetResult>(new List<EncounterTargetResult>(targetResults));
        }

        public bool Consumed { get; }

        public bool Duplicate { get; }

        public IReadOnlyList<RequirementResult> RequirementResults { get; }

        public IReadOnlyList<EncounterTargetResult> TargetResults { get; }
    }
}
