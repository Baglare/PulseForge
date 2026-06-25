using System;

namespace PulseForge.Domain.Rhythm
{
    public sealed class JudgementWindows
    {
        public JudgementWindows(double perfectWindowSeconds, double goodWindowSeconds)
        {
            if (!IsFinite(perfectWindowSeconds) || perfectWindowSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(perfectWindowSeconds), "Perfect window must be finite and greater than zero.");
            }

            if (!IsFinite(goodWindowSeconds) || goodWindowSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(goodWindowSeconds), "Good window must be finite and greater than zero.");
            }

            if (perfectWindowSeconds > goodWindowSeconds)
            {
                throw new ArgumentException("Perfect window must be less than or equal to good window.", nameof(perfectWindowSeconds));
            }

            PerfectWindowSeconds = perfectWindowSeconds;
            GoodWindowSeconds = goodWindowSeconds;
        }

        public double PerfectWindowSeconds { get; }

        public double GoodWindowSeconds { get; }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
