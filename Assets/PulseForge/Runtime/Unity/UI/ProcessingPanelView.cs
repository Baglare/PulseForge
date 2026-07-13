using System.Collections.Generic;
using PulseForge.Runtime.Unity.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class ProcessingPanelView : PulseForgePanelView
    {
        private static readonly PulseForgeProcessingStage[] Stages =
        {
            PulseForgeProcessingStage.AudioSelected,
            PulseForgeProcessingStage.ConvertingToWav,
            PulseForgeProcessingStage.LoadingConvertedAudio,
            PulseForgeProcessingStage.DetectingRhythm,
            PulseForgeProcessingStage.BuildingCombatSequence,
            PulseForgeProcessingStage.Ready
        };

        private static readonly string[] StageLabels =
        {
            "Track selected",
            "Converting to WAV",
            "Loading track",
            "Detecting rhythm",
            "Building combat sequence",
            "Ready to play"
        };

        [SerializeField] private Text songText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text[] stageTexts;

        public static ProcessingPanelView Create(Transform parent)
        {
            RectTransform card = FlowPanelBuilder.CreateScreenWithCard(
                "Processing Panel",
                parent,
                new Vector2(760f, 720f),
                out GameObject root);
            ProcessingPanelView view = root.AddComponent<ProcessingPanelView>();
            view.ConfigurePanelRoot(root);

            Text title = FlowPanelBuilder.AddHeading(card, "Preparing Your Track");
            PulseForgeUIFactory.SetLayoutHeight(title, 82f);
            view.songText = FlowPanelBuilder.AddCenteredText(card, string.Empty, 24, PulseForgeUITheme.PrimaryText, 52f);
            view.statusText = FlowPanelBuilder.AddCenteredText(card, string.Empty, 20, PulseForgeUITheme.SecondaryText, 62f);
            view.stageTexts = new Text[Stages.Length];
            for (int i = 0; i < view.stageTexts.Length; i++)
            {
                view.stageTexts[i] = FlowPanelBuilder.AddCenteredText(
                    card,
                    StageLabels[i],
                    24,
                    PulseForgeUITheme.SecondaryText,
                    54f);
            }

            return view;
        }

        public void Refresh(DebugRhythmPrototypeController controller)
        {
            songText.text = controller.SelectedAudioFileName;
            PulseForgeProcessingStage currentStage = controller.ProcessingStage;
            statusText.text = GetFriendlyStatus(currentStage);
            for (int i = 0; i < Stages.Length; i++)
            {
                if ((int)Stages[i] < (int)currentStage)
                {
                    stageTexts[i].text = "✓   " + StageLabels[i];
                    stageTexts[i].color = PulseForgeUITheme.Good;
                }
                else if (Stages[i] == currentStage)
                {
                    stageTexts[i].text = "●   " + StageLabels[i];
                    stageTexts[i].color = PulseForgeUITheme.Primary;
                }
                else
                {
                    stageTexts[i].text = "○   " + StageLabels[i];
                    stageTexts[i].color = PulseForgeUITheme.SecondaryText;
                }
            }
        }

        private static string GetFriendlyStatus(PulseForgeProcessingStage stage)
        {
            switch (stage)
            {
                case PulseForgeProcessingStage.AudioSelected:
                    return "Track received. Preparing the audio pipeline.";
                case PulseForgeProcessingStage.ConvertingToWav:
                    return "Converting your track to a playable format.";
                case PulseForgeProcessingStage.LoadingConvertedAudio:
                    return "Loading the prepared track.";
                case PulseForgeProcessingStage.DetectingRhythm:
                    return "Finding playable rhythm events.";
                case PulseForgeProcessingStage.BuildingCombatSequence:
                    return "Building the rhythm-combat sequence.";
                default:
                    return "Your track is ready.";
            }
        }

        public override void CollectValidationErrors(List<string> errors)
        {
            base.CollectValidationErrors(errors);
            PulseForgeUIValidation.AddMissing(errors, songText, "Processing: song text is missing.");
            PulseForgeUIValidation.AddMissing(errors, statusText, "Processing: status text is missing.");
            PulseForgeUIValidation.AddArray(errors, stageTexts, Stages.Length, "Processing: stage list is incomplete.");
        }
    }
}
