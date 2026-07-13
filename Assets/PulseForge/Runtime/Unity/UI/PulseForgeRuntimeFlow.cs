using System;
using PulseForge.Domain.Rhythm;

namespace PulseForge.Runtime.Unity.UI
{
    public enum PulseForgeUIState
    {
        Setup,
        Processing,
        Ready,
        Countdown,
        Playing,
        Paused,
        Completed,
        Error
    }

    public enum PulseForgeProcessingStage
    {
        None,
        AudioSelected,
        ConvertingToWav,
        LoadingConvertedAudio,
        DetectingRhythm,
        BuildingCombatSequence,
        Ready
    }

    public sealed class PulseForgeRuntimeFlow
    {
        public PulseForgeUIState State { get; private set; } = PulseForgeUIState.Setup;

        public PulseForgeProcessingStage ProcessingStage { get; private set; } = PulseForgeProcessingStage.None;

        public string SelectedAudioPath { get; private set; } = string.Empty;

        public string ErrorMessage { get; private set; } = string.Empty;

        public bool SelectAudioPath(string selectedAudioPath)
        {
            if (string.IsNullOrWhiteSpace(selectedAudioPath)
                || State == PulseForgeUIState.Processing
                || State == PulseForgeUIState.Countdown
                || State == PulseForgeUIState.Playing
                || State == PulseForgeUIState.Paused)
            {
                return false;
            }

            SelectedAudioPath = selectedAudioPath;
            ErrorMessage = string.Empty;
            ProcessingStage = PulseForgeProcessingStage.AudioSelected;
            State = PulseForgeUIState.Setup;
            return true;
        }

        public bool BeginProcessing()
        {
            if (string.IsNullOrWhiteSpace(SelectedAudioPath)
                || State == PulseForgeUIState.Processing
                || State == PulseForgeUIState.Countdown
                || State == PulseForgeUIState.Playing
                || State == PulseForgeUIState.Paused)
            {
                return false;
            }

            ErrorMessage = string.Empty;
            ProcessingStage = PulseForgeProcessingStage.AudioSelected;
            State = PulseForgeUIState.Processing;
            return true;
        }

        public void SetProcessingStage(PulseForgeProcessingStage stage)
        {
            if (State != PulseForgeUIState.Processing)
            {
                return;
            }

            ProcessingStage = stage;
        }

        public void MarkReady()
        {
            ErrorMessage = string.Empty;
            ProcessingStage = PulseForgeProcessingStage.Ready;
            State = PulseForgeUIState.Ready;
        }

        public void BeginSession(bool useCountdown)
        {
            if (State != PulseForgeUIState.Ready && State != PulseForgeUIState.Completed)
            {
                return;
            }

            ErrorMessage = string.Empty;
            State = useCountdown ? PulseForgeUIState.Countdown : PulseForgeUIState.Playing;
        }

        public void MarkPlaying()
        {
            if (State == PulseForgeUIState.Countdown)
            {
                State = PulseForgeUIState.Playing;
            }
        }

        public void Pause()
        {
            if (State == PulseForgeUIState.Playing)
            {
                State = PulseForgeUIState.Paused;
            }
        }

        public void Resume()
        {
            if (State == PulseForgeUIState.Paused)
            {
                State = PulseForgeUIState.Playing;
            }
        }

        public void Complete()
        {
            if (State == PulseForgeUIState.Playing || State == PulseForgeUIState.Paused)
            {
                State = PulseForgeUIState.Completed;
            }
        }

        public void MarkError(string errorMessage)
        {
            ErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
                ? "The session could not be prepared."
                : errorMessage.Trim();
            State = PulseForgeUIState.Error;
        }

        public void ReturnToSetup(bool clearSelectedAudio)
        {
            if (clearSelectedAudio)
            {
                SelectedAudioPath = string.Empty;
                ProcessingStage = PulseForgeProcessingStage.None;
            }
            else
            {
                ProcessingStage = string.IsNullOrWhiteSpace(SelectedAudioPath)
                    ? PulseForgeProcessingStage.None
                    : PulseForgeProcessingStage.AudioSelected;
            }

            ErrorMessage = string.Empty;
            State = PulseForgeUIState.Setup;
        }
    }

    public readonly struct PulseForgeFeedbackPresentation
    {
        public PulseForgeFeedbackPresentation(
            bool isVisible,
            string text,
            RhythmAction? action,
            HitGrade? grade,
            float alpha)
        {
            IsVisible = isVisible;
            Text = text ?? string.Empty;
            Action = action;
            Grade = grade;
            Alpha = Math.Max(0f, Math.Min(1f, alpha));
        }

        public bool IsVisible { get; }

        public string Text { get; }

        public RhythmAction? Action { get; }

        public HitGrade? Grade { get; }

        public float Alpha { get; }

        public static PulseForgeFeedbackPresentation Hidden
        {
            get { return new PulseForgeFeedbackPresentation(false, string.Empty, null, null, 0f); }
        }
    }
}
