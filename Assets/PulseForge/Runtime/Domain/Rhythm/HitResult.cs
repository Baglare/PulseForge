namespace PulseForge.Domain.Rhythm
{
    public sealed class HitResult
    {
        public HitResult(string eventId, HitGrade grade, double timingErrorSeconds)
        {
            EventId = eventId;
            Grade = grade;
            TimingErrorSeconds = timingErrorSeconds;
        }

        public string EventId { get; }

        public HitGrade Grade { get; }

        public double TimingErrorSeconds { get; }
    }
}
