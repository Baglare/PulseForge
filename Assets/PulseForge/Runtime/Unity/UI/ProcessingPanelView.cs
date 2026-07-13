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
            "Audio selected",
            "Converting to WAV",
            "Loading converted audio",
            "Detecting rhythm",
            "Building combat sequence",
            "Ready"
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

            Text title = FlowPanelBuilder.AddHeading(card, "Preparing your session");
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
            statusText.text = controller.RuntimeAudioImportStatus;
            PulseForgeProcessingStage currentStage = controller.ProcessingStage;
            for (int i = 0; i < Stages.Length; i++)
            {
                if ((int)Stages[i] < (int)currentStage)
                {
                    stageTexts[i].text = "Completed   " + StageLabels[i];
                    stageTexts[i].color = PulseForgeUITheme.Good;
                }
                else if (Stages[i] == currentStage)
                {
                    stageTexts[i].text = "In progress   " + StageLabels[i];
                    stageTexts[i].color = PulseForgeUITheme.PrimaryText;
                }
                else
                {
                    stageTexts[i].text = "Waiting   " + StageLabels[i];
                    stageTexts[i].color = PulseForgeUITheme.SecondaryText;
                }
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
