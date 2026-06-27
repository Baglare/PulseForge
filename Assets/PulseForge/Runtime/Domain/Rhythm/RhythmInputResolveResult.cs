using System;

namespace PulseForge.Domain.Rhythm
{
    public sealed class RhythmInputResolveResult
    {
        private RhythmInputResolveResult(bool hasMatch, BeatEventRuntime matchedEvent, HitResult hitResult)
        {
            HasMatch = hasMatch;
            MatchedEvent = matchedEvent;
            HitResult = hitResult;
        }

        public bool HasMatch { get; }

        public BeatEventRuntime MatchedEvent { get; }

        public HitResult HitResult { get; }

        public static RhythmInputResolveResult NoMatch()
        {
            return new RhythmInputResolveResult(false, null, null);
        }

        public static RhythmInputResolveResult Matched(BeatEventRuntime matchedEvent, HitResult hitResult)
        {
            if (matchedEvent == null)
            {
                throw new ArgumentNullException(nameof(matchedEvent));
            }

            if (hitResult == null)
            {
                throw new ArgumentNullException(nameof(hitResult));
            }

            return new RhythmInputResolveResult(true, matchedEvent, hitResult);
        }
    }
}
