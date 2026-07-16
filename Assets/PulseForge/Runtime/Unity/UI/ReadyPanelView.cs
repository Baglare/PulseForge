using System.Collections.Generic;
using System;
using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class ReadyPanelView : PulseForgePanelView
    {
        [SerializeField] private Text songText;
        [SerializeField] private Text eventCountText;
        [SerializeField] private Text inputCostText;
        [SerializeField] private Text analysisQualityText;
        [SerializeField] private Text detectionText;
        [SerializeField] private Text difficultyText;
        [SerializeField] private Text combatStyleText;
        [SerializeField] private Button[] gameModeButtons;
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button anotherSongButton;

        private DebugRhythmPrototypeController boundController;
        private ChoiceSelector<RadialGameMode> gameModeSelector;

        public static ReadyPanelView Create(Transform parent)
        {
            RectTransform card = FlowPanelBuilder.CreateScreenWithCard(
                "Ready Panel",
                parent,
                new Vector2(820f, 780f),
                out GameObject root);
            ReadyPanelView view = root.AddComponent<ReadyPanelView>();
            view.ConfigurePanelRoot(root);

            FlowPanelBuilder.AddHeading(card, "Track Ready");
            view.songText = FlowPanelBuilder.AddValue(card, "Song");
            view.eventCountText = FlowPanelBuilder.AddValue(card, "Events");
            view.inputCostText = FlowPanelBuilder.AddValue(card, "Input Cost");
            view.analysisQualityText = FlowPanelBuilder.AddValue(card, "Analysis Quality");
            view.detectionText = FlowPanelBuilder.AddValue(card, "Detection");
            view.difficultyText = FlowPanelBuilder.AddValue(card, "Difficulty");
            view.combatStyleText = FlowPanelBuilder.AddValue(card, "Combat Style");
            view.gameModeButtons = SetupPanelView.CreateChoiceButtons(
                card,
                "Game Mode",
                new[] { "Standard", "Survival", "One Life" });
            view.startButton = FlowPanelBuilder.AddButton(card, "Start", PulseForgeUITheme.Primary, 76f, 30);
            view.settingsButton = FlowPanelBuilder.AddButton(card, "Change Settings", PulseForgeUITheme.SecondaryText);
            view.anotherSongButton = FlowPanelBuilder.AddButton(card, "Choose Another Song", PulseForgeUITheme.SurfaceSoft);
            return view;
        }

        public void EnsureV2SummaryFields(Action<GameObject> registerCreated = null)
        {
            RectTransform card = PanelRoot == null
                ? null
                : PanelRoot.transform.Find("Ready Panel Card") as RectTransform;
            if (card == null) return;

            Transform inputCost = card.Find("Input Cost");
            inputCostText = inputCost == null ? null : inputCost.GetComponent<Text>();
            if (inputCostText == null)
            {
                inputCostText = FlowPanelBuilder.AddValue(card, "Input Cost");
                registerCreated?.Invoke(inputCostText.gameObject);
            }

            int inputCostIndex = eventCountText == null
                ? Math.Max(0, Math.Min(3, card.childCount - 1))
                : eventCountText.transform.GetSiblingIndex() + 1;
            inputCostText.transform.SetSiblingIndex(inputCostIndex);

            Transform analysisQuality = card.Find("Analysis Quality");
            analysisQualityText = analysisQuality == null ? null : analysisQuality.GetComponent<Text>();
            if (analysisQualityText == null)
            {
                analysisQualityText = FlowPanelBuilder.AddValue(card, "Analysis Quality");
                registerCreated?.Invoke(analysisQualityText.gameObject);
            }
            analysisQualityText.transform.SetSiblingIndex(inputCostText.transform.GetSiblingIndex() + 1);
        }

        public void EnsureGameModeControls(Action<GameObject> registerCreated = null)
        {
            Transform card = PanelRoot == null ? null : PanelRoot.transform.Find("Ready Panel Card");
            if (card == null) return;
            Transform selector = card.Find("Game Mode Selector");
            if (selector == null)
            {
                gameModeButtons = SetupPanelView.CreateChoiceButtons(
                    card,
                    "Game Mode",
                    new[] { "Standard", "Survival", "One Life" });
                selector = card.Find("Game Mode Selector");
                if (selector != null && startButton != null)
                {
                    selector.SetSiblingIndex(startButton.transform.GetSiblingIndex());
                    registerCreated?.Invoke(selector.gameObject);
                }
            }
            else
            {
                gameModeButtons = selector.GetComponentsInChildren<Button>(true);
            }

            RectTransform cardRect = card as RectTransform;
            if (cardRect != null && cardRect.sizeDelta.y < 980f)
            {
                cardRect.sizeDelta = new Vector2(cardRect.sizeDelta.x, 980f);
            }
        }

        public void Bind(DebugRhythmPrototypeController controller)
        {
            if (boundController == controller)
            {
                return;
            }

            Unbind();
            boundController = controller;
            if (controller == null)
            {
                return;
            }

            PulseForgeUIFactory.BindButton(startButton, controller.StartSession);
            PulseForgeUIFactory.BindButton(settingsButton, controller.ChangeSettings);
            PulseForgeUIFactory.BindButton(anotherSongButton, controller.ChooseAnotherSong);
            gameModeSelector = new ChoiceSelector<RadialGameMode>(
                gameModeButtons,
                new[] { RadialGameMode.Standard, RadialGameMode.Survival, RadialGameMode.OneLife },
                PulseForgeUITheme.Primary,
                controller.SetGameMode);
        }

        public void Unbind()
        {
            PulseForgeUIFactory.UnbindButton(startButton);
            PulseForgeUIFactory.UnbindButton(settingsButton);
            PulseForgeUIFactory.UnbindButton(anotherSongButton);
            gameModeSelector?.Unbind();
            gameModeSelector = null;
            boundController = null;
        }

        public void Refresh(DebugRhythmPrototypeController controller)
        {
            songText.text = controller.SongName;
            eventCountText.text = "EVENTS     " + controller.SessionEventCount;
            inputCostText.text = "INPUT COST     " + controller.SessionInputCost;
            analysisQualityText.text = "ANALYSIS     " + controller.AnalysisQualitySummary;
            detectionText.text = "DETECTION     " + controller.AppliedDetectionLabel;
            difficultyText.text = "DIFFICULTY     " + controller.AppliedDifficultyLabel;
            combatStyleText.text = "COMBAT STYLE     " + controller.AppliedCombatStyleLabel;
            gameModeSelector?.SetSelected(controller.SelectedGameMode);
            startButton.interactable = controller.CanStart;
        }

        public override void CollectValidationErrors(List<string> errors)
        {
            base.CollectValidationErrors(errors);
            PulseForgeUIValidation.AddMissing(errors, songText, "Ready: song text is missing.");
            PulseForgeUIValidation.AddMissing(errors, eventCountText, "Ready: event count text is missing.");
            PulseForgeUIValidation.AddMissing(errors, inputCostText, "Ready: input cost text is missing.");
            PulseForgeUIValidation.AddMissing(errors, analysisQualityText, "Ready: analysis quality text is missing.");
            PulseForgeUIValidation.AddMissing(errors, detectionText, "Ready: detection text is missing.");
            PulseForgeUIValidation.AddMissing(errors, difficultyText, "Ready: difficulty text is missing.");
            PulseForgeUIValidation.AddMissing(errors, combatStyleText, "Ready: combat style text is missing.");
            PulseForgeUIValidation.AddArray(errors, gameModeButtons, 3, "Ready: game mode buttons are incomplete.");
            PulseForgeUIValidation.AddMissing(errors, startButton, "Ready: Start button is missing.");
            PulseForgeUIValidation.AddMissing(errors, settingsButton, "Ready: Change Settings button is missing.");
            PulseForgeUIValidation.AddMissing(errors, anotherSongButton, "Ready: Choose Another Song button is missing.");
        }
    }
}
