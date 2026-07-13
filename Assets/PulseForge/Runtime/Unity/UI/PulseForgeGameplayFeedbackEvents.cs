using PulseForge.Domain.Rhythm;

namespace PulseForge.Runtime.Unity.UI
{
    public readonly struct PulseForgeGameplayResultEvent
    {
        public PulseForgeGameplayResultEvent(
            long sequenceId,
            string eventId,
            RhythmAction action,
            HitGrade grade,
            float intensity,
            int previousCombo,
            int currentCombo)
        {
            SequenceId = sequenceId;
            EventId = eventId;
            Action = action;
            Grade = grade;
            Intensity = intensity;
            PreviousCombo = previousCombo;
            CurrentCombo = currentCombo;
        }

        public long SequenceId { get; }
        public string EventId { get; }
        public RhythmAction Action { get; }
        public HitGrade Grade { get; }
        public float Intensity { get; }
        public int PreviousCombo { get; }
        public int CurrentCombo { get; }
    }

    public readonly struct PulseForgeComboChangedEvent
    {
        public PulseForgeComboChangedEvent(long sequenceId, int previousCombo, int currentCombo)
        {
            SequenceId = sequenceId;
            PreviousCombo = previousCombo;
            CurrentCombo = currentCombo;
        }

        public long SequenceId { get; }
        public int PreviousCombo { get; }
        public int CurrentCombo { get; }
    }
}
