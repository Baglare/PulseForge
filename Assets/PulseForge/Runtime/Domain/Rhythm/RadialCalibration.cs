using System;
using System.Collections.Generic;

namespace PulseForge.Domain.Rhythm
{
    public enum CalibrationConfidence
    {
        Low,
        Medium,
        High
    }

    public readonly struct RadialInputCalibrationResult
    {
        public RadialInputCalibrationResult(
            double suggestedInputOffsetSeconds,
            int validSampleCount,
            double medianDeviationMilliseconds,
            double jitterMilliseconds,
            CalibrationConfidence confidence)
        {
            SuggestedInputOffsetSeconds = suggestedInputOffsetSeconds;
            ValidSampleCount = validSampleCount;
            MedianDeviationMilliseconds = medianDeviationMilliseconds;
            JitterMilliseconds = jitterMilliseconds;
            Confidence = confidence;
        }

        public double SuggestedInputOffsetSeconds { get; }
        public int ValidSampleCount { get; }
        public double MedianDeviationMilliseconds { get; }
        public double JitterMilliseconds { get; }
        public CalibrationConfidence Confidence { get; }
    }

    public static class RadialInputCalibration
    {
        public const int WarmUpBeatCount = 2;
        public const int MeasurementBeatCount = 12;
        public const int MinimumValidSampleCount = 8;

        public static bool TryAnalyze(
            IReadOnlyList<double> rawInputTimesSeconds,
            IReadOnlyList<double> targetTimesSeconds,
            double currentInputOffsetSeconds,
            out RadialInputCalibrationResult result)
        {
            result = default(RadialInputCalibrationResult);
            if (rawInputTimesSeconds == null
                || targetTimesSeconds == null
                || rawInputTimesSeconds.Count != targetTimesSeconds.Count)
            {
                return false;
            }

            List<double> deviations = new List<double>(rawInputTimesSeconds.Count);
            for (int i = 0; i < rawInputTimesSeconds.Count; i++)
            {
                double raw = rawInputTimesSeconds[i];
                double target = targetTimesSeconds[i];
                if (!IsFinite(raw) || !IsFinite(target))
                {
                    continue;
                }
                double effective = RadialTimingMath.EffectiveJudgementTimeSeconds(
                    raw,
                    currentInputOffsetSeconds);
                deviations.Add(effective - target);
            }

            if (deviations.Count < MinimumValidSampleCount)
            {
                return false;
            }

            double initialMedian = Median(deviations);
            List<double> absoluteDeviations = AbsoluteDeviations(deviations, initialMedian);
            double mad = Median(absoluteDeviations);
            double threshold = Math.Max(0.001d, 3d * 1.4826d * mad);
            List<double> filtered = new List<double>(deviations.Count);
            for (int i = 0; i < deviations.Count; i++)
            {
                if (Math.Abs(deviations[i] - initialMedian) <= threshold)
                {
                    filtered.Add(deviations[i]);
                }
            }

            if (filtered.Count < MinimumValidSampleCount)
            {
                return false;
            }

            double robustMedian = Median(filtered);
            double jitter = 1.4826d * Median(AbsoluteDeviations(filtered, robustMedian));
            CalibrationConfidence confidence = filtered.Count >= 10 && jitter <= 0.015d
                ? CalibrationConfidence.High
                : jitter <= 0.030d
                    ? CalibrationConfidence.Medium
                    : CalibrationConfidence.Low;
            result = new RadialInputCalibrationResult(
                RadialTimingMath.InputOffsetForObservedDelta(
                    currentInputOffsetSeconds,
                    robustMedian),
                filtered.Count,
                robustMedian * 1000d,
                jitter * 1000d,
                confidence);
            return true;
        }

        private static List<double> AbsoluteDeviations(
            IReadOnlyList<double> values,
            double median)
        {
            List<double> deviations = new List<double>(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                deviations.Add(Math.Abs(values[i] - median));
            }
            return deviations;
        }

        private static double Median(IReadOnlyList<double> values)
        {
            double[] sorted = new double[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                sorted[i] = values[i];
            }
            Array.Sort(sorted);
            int middle = sorted.Length / 2;
            return sorted.Length % 2 == 0
                ? (sorted[middle - 1] + sorted[middle]) * 0.5d
                : sorted[middle];
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    public sealed class CalibrationOffsetDraft
    {
        public CalibrationOffsetDraft(
            double beatMapOffsetSeconds,
            double inputOffsetSeconds)
        {
            OriginalBeatMapOffsetSeconds = beatMapOffsetSeconds;
            OriginalInputOffsetSeconds = inputOffsetSeconds;
            BeatMapOffsetSeconds = beatMapOffsetSeconds;
            InputOffsetSeconds = inputOffsetSeconds;
        }

        public double OriginalBeatMapOffsetSeconds { get; }
        public double OriginalInputOffsetSeconds { get; }
        public double BeatMapOffsetSeconds { get; private set; }
        public double InputOffsetSeconds { get; private set; }
        public bool IsCommitted { get; private set; }

        public void StageBeatMapOffset(double value)
        {
            BeatMapOffsetSeconds = ClampOffset(value);
        }

        public void StageInputOffset(double value)
        {
            InputOffsetSeconds = ClampOffset(value);
        }

        public void Commit()
        {
            IsCommitted = true;
        }

        public void Cancel()
        {
            BeatMapOffsetSeconds = OriginalBeatMapOffsetSeconds;
            InputOffsetSeconds = OriginalInputOffsetSeconds;
            IsCommitted = false;
        }

        private static double ClampOffset(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            return Math.Max(-0.5d, Math.Min(0.5d, value));
        }
    }
}
