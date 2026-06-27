namespace PulseForge.Domain.Rhythm
{
    public sealed class ScoreSnapshot
    {
        public ScoreSnapshot(
            int totalScore,
            int currentCombo,
            int maxCombo,
            int perfectCount,
            int goodCount,
            int missCount,
            int totalJudgedCount)
        {
            TotalScore = totalScore;
            CurrentCombo = currentCombo;
            MaxCombo = maxCombo;
            PerfectCount = perfectCount;
            GoodCount = goodCount;
            MissCount = missCount;
            TotalJudgedCount = totalJudgedCount;
        }

        public int TotalScore { get; }

        public int CurrentCombo { get; }

        public int MaxCombo { get; }

        public int PerfectCount { get; }

        public int GoodCount { get; }

        public int MissCount { get; }

        public int TotalJudgedCount { get; }
    }
}
