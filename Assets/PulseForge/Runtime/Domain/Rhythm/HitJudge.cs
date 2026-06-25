using System;

namespace PulseForge.Domain.Rhythm
{
    public sealed class HitJudge
    {
        private const double BoundaryToleranceSeconds = 0.000000000001d;

        public HitResult Judge(double inputTimeSeconds, BeatEventData beatEvent, JudgementWindows windows)
        {
            if (!IsFinite(inputTimeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(inputTimeSeconds), "Input time must be finite.");
            }

            if (beatEvent == null)
            {
                throw new ArgumentNullException(nameof(beatEvent));
            }

            if (windows == null)
            {
                throw new ArgumentNullException(nameof(windows));
            }

            double error = inputTimeSeconds - beatEvent.TargetTimeSeconds;
            double absoluteError = Math.Abs(error);
            HitGrade grade = Classify(absoluteError, windows);

            return new HitResult(beatEvent.EventId, grade, error);
        }

        private static HitGrade Classify(double absoluteError, JudgementWindows windows)
        {
            if (absoluteError <= windows.PerfectWindowSeconds + BoundaryToleranceSeconds)
            {
                return HitGrade.Perfect;
            }

            if (absoluteError <= windows.GoodWindowSeconds + BoundaryToleranceSeconds)
            {
                return HitGrade.Good;
            }

            return HitGrade.Miss;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
