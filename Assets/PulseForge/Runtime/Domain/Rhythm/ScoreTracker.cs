using System;
using System.Collections.Generic;

namespace PulseForge.Domain.Rhythm
{
    public sealed class ScoreTracker
    {
        private const int PerfectScore = 1000;
        private const int GoodScore = 500;

        private readonly HashSet<string> recordedEventIds = new HashSet<string>(StringComparer.Ordinal);

        public int TotalScore { get; private set; }

        public int CurrentCombo { get; private set; }

        public int MaxCombo { get; private set; }

        public int PerfectCount { get; private set; }

        public int GoodCount { get; private set; }

        public int MissCount { get; private set; }

        public int TotalJudgedCount
        {
            get { return PerfectCount + GoodCount + MissCount; }
        }

        public void Record(HitResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (!recordedEventIds.Add(result.EventId))
            {
                throw new InvalidOperationException("Hit result for this event has already been recorded.");
            }

            ApplyGrade(result.Grade);
        }

        public ScoreSnapshot CreateSnapshot()
        {
            return new ScoreSnapshot(
                TotalScore,
                CurrentCombo,
                MaxCombo,
                PerfectCount,
                GoodCount,
                MissCount,
                TotalJudgedCount);
        }

        public void Reset()
        {
            TotalScore = 0;
            CurrentCombo = 0;
            MaxCombo = 0;
            PerfectCount = 0;
            GoodCount = 0;
            MissCount = 0;
            recordedEventIds.Clear();
        }

        private void ApplyGrade(HitGrade grade)
        {
            switch (grade)
            {
                case HitGrade.Perfect:
                    PerfectCount++;
                    TotalScore += PerfectScore;
                    IncreaseCombo();
                    break;
                case HitGrade.Good:
                    GoodCount++;
                    TotalScore += GoodScore;
                    IncreaseCombo();
                    break;
                case HitGrade.Miss:
                    MissCount++;
                    CurrentCombo = 0;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(grade), grade, "Unsupported hit grade.");
            }
        }

        private void IncreaseCombo()
        {
            CurrentCombo++;
            if (CurrentCombo > MaxCombo)
            {
                MaxCombo = CurrentCombo;
            }
        }
    }
}
