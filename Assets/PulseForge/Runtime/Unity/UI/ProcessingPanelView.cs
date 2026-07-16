using System;
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
            PulseForgeProcessingStage.AnalyzingAudioFeatures,
            PulseForgeProcessingStage.PlanningRadialEncounters,
            PulseForgeProcessingStage.ValidatingBeatMap,
            PulseForgeProcessingStage.PreparingSession,
            PulseForgeProcessingStage.Ready
        };

        private static readonly string[] StageLabels =
        {
            "Track selected",
            "Converting to WAV",
            "Loading track",
            "Analyzing audio features",
            "Planning radial encounters",
            "Validating beatmap",
            "Preparing session",
            "Ready to play"
        };

        private static readonly string[] LegacyStageNames =
        {
            "Audio selected",
            "Converting to WAV",
            "Loading converted audio",
            "Detecting rhythm",
            "Building combat sequence",
            null,
            null,
            "Ready"
        };

        [SerializeField] private Text songText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text[] stageTexts;

        private PulseForgeProcessingStage currentStage = PulseForgeProcessingStage.None;

        public PulseForgeProcessingStage CurrentStage => currentStage;
        public Text[] StageTexts => stageTexts ?? Array.Empty<Text>();

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

            ApplyCompactLayout(card, view.songText, view.statusText, view.stageTexts);

            return view;
        }

        public void EnsureV2Stages(Action<GameObject> registerCreated = null)
        {
            Transform card = PanelRoot == null ? null : PanelRoot.transform.Find("Processing Panel Card");
            if (card == null) return;

            Text[] orderedStages = new Text[Stages.Length];
            int firstStageIndex = card.childCount;
            for (int i = 0; i < StageLabels.Length; i++)
            {
                Transform existing = card.Find(StageLabels[i]);
                if (existing != null)
                {
                    orderedStages[i] = existing.GetComponent<Text>();
                    firstStageIndex = Math.Min(firstStageIndex, existing.GetSiblingIndex());
                    continue;
                }

                string legacyName = LegacyStageNames[i];
                if (!string.IsNullOrEmpty(legacyName))
                {
                    existing = card.Find(legacyName);
                    if (existing != null)
                    {
                        orderedStages[i] = existing.GetComponent<Text>();
                        firstStageIndex = Math.Min(firstStageIndex, existing.GetSiblingIndex());
                    }
                }
            }

            if (firstStageIndex == card.childCount)
            {
                firstStageIndex = Math.Min(3, card.childCount);
            }

            RectTransform cardRect = card as RectTransform;
            for (int i = 0; i < StageLabels.Length; i++)
            {
                if (orderedStages[i] == null)
                {
                    orderedStages[i] = FlowPanelBuilder.AddCenteredText(
                        cardRect,
                        StageLabels[i],
                        24,
                        PulseForgeUITheme.SecondaryText,
                        54f);
                    registerCreated?.Invoke(orderedStages[i].gameObject);
                }
                orderedStages[i].transform.SetSiblingIndex(firstStageIndex + i);
            }
            stageTexts = orderedStages;
            ApplyCompactLayout(cardRect, songText, statusText, stageTexts);
        }

        private static void ApplyCompactLayout(
            RectTransform card,
            Text song,
            Text status,
            Text[] stages)
        {
            if (card == null)
            {
                return;
            }

            VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.padding = new RectOffset(52, 52, 28, 28);
                layout.spacing = 6f;
            }

            Transform heading = card.Find("Heading");
            if (heading != null)
            {
                PulseForgeUIFactory.SetLayoutHeight(heading, 58f);
            }

            if (song != null)
            {
                PulseForgeUIFactory.SetLayoutHeight(song, 38f);
            }

            if (status != null)
            {
                PulseForgeUIFactory.SetLayoutHeight(status, 42f);
            }

            for (int i = 0; i < stages.Length; i++)
            {
                if (stages[i] == null)
                {
                    continue;
                }

                stages[i].fontSize = 20;
                PulseForgeUIFactory.SetLayoutHeight(stages[i], 38f);
            }
        }

        public void Refresh(DebugRhythmPrototypeController controller)
        {
            songText.text = controller.SelectedAudioFileName;
            PulseForgeProcessingStage currentStage = controller.ProcessingStage;
            this.currentStage = currentStage;
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

        public Text GetStageText(PulseForgeProcessingStage stage)
        {
            for (int i = 0; i < Stages.Length; i++)
            {
                if (Stages[i] == stage && stageTexts != null && i < stageTexts.Length)
                {
                    return stageTexts[i];
                }
            }

            return null;
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
                case PulseForgeProcessingStage.AnalyzingAudioFeatures:
                    return "Extracting adaptive rhythm and section features.";
                case PulseForgeProcessingStage.PlanningRadialEncounters:
                    return "Planning the radial encounter sequence.";
                case PulseForgeProcessingStage.ValidatingBeatMap:
                    return "Checking density and playability.";
                case PulseForgeProcessingStage.PreparingSession:
                    return "Preparing the radial rhythm session.";
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
