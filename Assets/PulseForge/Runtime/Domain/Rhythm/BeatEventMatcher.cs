using System;
using System.Collections.Generic;

namespace PulseForge.Domain.Rhythm
{
    public sealed class BeatEventMatcher
    {
        public bool TryFindBestMatch(
            IReadOnlyList<BeatEventRuntime> events,
            RhythmAction action,
            double inputTimeSeconds,
            JudgementWindows windows,
            out BeatEventRuntime match)
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

            match = null;
            double bestDistance = 0d;

            for (int i = 0; i < events.Count; i++)
            {
                BeatEventRuntime candidate = events[i];
                if (candidate == null)
                {
                    throw new ArgumentException("Events must not contain null elements.", nameof(events));
                }

                if (!IsCandidate(candidate, action, inputTimeSeconds, windows, out double distance))
                {
                    continue;
                }

                if (match == null || IsBetterMatch(candidate, distance, match, bestDistance))
                {
                    match = candidate;
                    bestDistance = distance;
                }
            }

            return match != null;
        }

        private static bool IsCandidate(
            BeatEventRuntime candidate,
            RhythmAction action,
            double inputTimeSeconds,
            JudgementWindows windows,
            out double distance)
        {
            distance = Math.Abs(inputTimeSeconds - candidate.Data.TargetTimeSeconds);

            return !candidate.IsResolved
                && candidate.Data.Action == action
                && distance <= windows.GoodWindowSeconds;
        }

        private static bool IsBetterMatch(
            BeatEventRuntime candidate,
            double candidateDistance,
            BeatEventRuntime current,
            double currentDistance)
        {
            int distanceComparison = candidateDistance.CompareTo(currentDistance);
            if (distanceComparison != 0)
            {
                return distanceComparison < 0;
            }

            int targetTimeComparison = candidate.Data.TargetTimeSeconds.CompareTo(current.Data.TargetTimeSeconds);
            if (targetTimeComparison != 0)
            {
                return targetTimeComparison < 0;
            }

            return string.CompareOrdinal(candidate.Data.EventId, current.Data.EventId) < 0;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
