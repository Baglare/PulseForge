using System;
using System.Collections.Generic;

namespace PulseForge.Domain.Rhythm
{
    public sealed class BeatEventTimeoutProcessor
    {
        private readonly HitJudge judge;

        public BeatEventTimeoutProcessor(HitJudge judge)
        {
            this.judge = judge ?? throw new ArgumentNullException(nameof(judge));
        }

        public IReadOnlyList<HitResult> MarkTimedOutEvents(
            IReadOnlyList<BeatEventRuntime> events,
            double currentTimeSeconds,
            JudgementWindows windows)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            if (windows == null)
            {
                throw new ArgumentNullException(nameof(windows));
            }

            if (!IsFinite(currentTimeSeconds) || currentTimeSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(currentTimeSeconds), "Current time must be finite and greater than or equal to zero.");
            }

            var timedOutResults = new List<HitResult>();

            for (int i = 0; i < events.Count; i++)
            {
                BeatEventRuntime beatEvent = events[i];
                if (beatEvent == null)
                {
                    throw new ArgumentException("Events must not contain null elements.", nameof(events));
                }

                if (!IsTimedOut(beatEvent, currentTimeSeconds, windows))
                {
                    continue;
                }

                HitResult result = judge.Judge(currentTimeSeconds, beatEvent.Data, windows);
                beatEvent.ApplyResult(result);
                timedOutResults.Add(result);
            }

            return timedOutResults;
        }

        private static bool IsTimedOut(BeatEventRuntime beatEvent, double currentTimeSeconds, JudgementWindows windows)
        {
            return !beatEvent.IsResolved
                && currentTimeSeconds > beatEvent.Data.TargetTimeSeconds + windows.GoodWindowSeconds;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
