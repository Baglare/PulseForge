using System;

namespace PulseForge.Domain.Rhythm
{
    public sealed class BeatEventData
    {
        public BeatEventData(string eventId, double targetTimeSeconds, RhythmAction action, float intensity)
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                throw new ArgumentException("Event id must not be null, empty, or whitespace.", nameof(eventId));
            }

            if (!IsFinite(targetTimeSeconds) || targetTimeSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(targetTimeSeconds), "Target time must be finite and greater than or equal to zero.");
            }

            if (!IsFinite(intensity) || intensity < 0f || intensity > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(intensity), "Intensity must be finite and within the inclusive range [0, 1].");
            }

            EventId = eventId;
            TargetTimeSeconds = targetTimeSeconds;
            Action = action;
            Intensity = intensity;
        }

        public string EventId { get; }

        public double TargetTimeSeconds { get; }

        public RhythmAction Action { get; }

        public float Intensity { get; }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
