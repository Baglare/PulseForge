using System;

namespace PulseForge.Domain.Rhythm
{
    public sealed class BeatEventRuntime
    {
        public BeatEventRuntime(BeatEventData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            State = BeatEventState.Pending;
        }

        public BeatEventData Data { get; }

        public BeatEventState State { get; private set; }

        public HitResult Result { get; private set; }

        public bool IsResolved
        {
            get { return State != BeatEventState.Pending; }
        }

        public void ApplyResult(HitResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (IsResolved)
            {
                throw new InvalidOperationException("Beat event has already been resolved.");
            }

            if (!string.Equals(result.EventId, Data.EventId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Result event id must match beat event id.", nameof(result));
            }

            State = ToResolvedState(result.Grade);
            Result = result;
        }

        private static BeatEventState ToResolvedState(HitGrade grade)
        {
            switch (grade)
            {
                case HitGrade.Perfect:
                case HitGrade.Good:
                    return BeatEventState.Hit;
                case HitGrade.Miss:
                    return BeatEventState.Missed;
                default:
                    throw new ArgumentOutOfRangeException(nameof(grade), grade, "Unsupported hit grade.");
            }
        }
    }
}
