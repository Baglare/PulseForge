using System;
using System.Collections.Generic;

namespace PulseForge.Domain.Rhythm
{
    public sealed class RhythmInputResolver
    {
        private readonly BeatEventMatcher matcher;
        private readonly HitJudge judge;

        public RhythmInputResolver(BeatEventMatcher matcher, HitJudge judge)
        {
            this.matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
            this.judge = judge ?? throw new ArgumentNullException(nameof(judge));
        }

        public RhythmInputResolveResult ResolveInput(
            IReadOnlyList<BeatEventRuntime> events,
            RhythmAction action,
            double inputTimeSeconds,
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

            if (!IsFinite(inputTimeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(inputTimeSeconds), "Input time must be finite.");
            }

            if (!matcher.TryFindBestMatch(events, action, inputTimeSeconds, windows, out BeatEventRuntime matchedEvent))
            {
                return RhythmInputResolveResult.NoMatch();
            }

            HitResult hitResult = judge.Judge(inputTimeSeconds, matchedEvent.Data, windows);
            matchedEvent.ApplyResult(hitResult);

            return RhythmInputResolveResult.Matched(matchedEvent, hitResult);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
