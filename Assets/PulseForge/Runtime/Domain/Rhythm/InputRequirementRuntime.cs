using System;

namespace PulseForge.Domain.Rhythm
{
    public sealed class InputRequirementRuntime
    {
        internal InputRequirementRuntime(InputRequirementData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public InputRequirementData Data { get; }

        public RequirementResult Result { get; private set; }

        public bool IsResolved => Result != null;

        public bool HasCapturedInput { get; private set; }

        public RhythmAction CapturedAction { get; private set; }

        public RhythmInputPhase CapturedPhase { get; private set; }

        public double CapturedTimeSeconds { get; private set; }

        public HitGrade CapturedGrade { get; private set; }

        public int AcceptedPressCount { get; private set; }

        public double LastAcceptedPressTimeSeconds { get; private set; }

        internal void Capture(RhythmInputSample input, HitGrade grade)
        {
            if (IsResolved)
            {
                throw new InvalidOperationException("A resolved requirement cannot capture more input.");
            }

            HasCapturedInput = true;
            CapturedAction = input.Action;
            CapturedPhase = input.Phase;
            CapturedTimeSeconds = input.SongTimeSeconds;
            CapturedGrade = grade;
        }

        internal void RegisterPress(double songTimeSeconds)
        {
            AcceptedPressCount++;
            LastAcceptedPressTimeSeconds = songTimeSeconds;
        }

        internal void Resolve(RequirementResult result)
        {
            if (IsResolved)
            {
                throw new InvalidOperationException("A requirement may only be resolved once.");
            }

            Result = result ?? throw new ArgumentNullException(nameof(result));
        }

        internal void Reset()
        {
            Result = null;
            HasCapturedInput = false;
            CapturedAction = default(RhythmAction);
            CapturedPhase = default(RhythmInputPhase);
            CapturedTimeSeconds = 0d;
            CapturedGrade = default(HitGrade);
            AcceptedPressCount = 0;
            LastAcceptedPressTimeSeconds = 0d;
        }
    }
}
