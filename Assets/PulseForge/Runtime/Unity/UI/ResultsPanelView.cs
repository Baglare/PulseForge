using System.Collections.Generic;
using System;
using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class ResultsPanelView : PulseForgePanelView
    {
        [SerializeField] private Text headingText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text maxComboText;
        [SerializeField] private Text perfectText;
        [SerializeField] private Text goodText;
        [SerializeField] private Text missText;
        [SerializeField] private Text settingsText;
        [SerializeField] private Text gameModeText;
        [SerializeField] private Text healthText;
        [SerializeField] private Text failureText;
        [SerializeField] private Button replayButton;
        [SerializeField] private Button anotherSongButton;

        private DebugRhythmPrototypeController boundController;

        public static ResultsPanelView Create(Transform parent)
        {
            RectTransform card = FlowPanelBuilder.CreateScreenWithCard(
                "Results Panel",
                parent,
                new Vector2(840f, 950f),
                out GameObject root);
            ResultsPanelView view = root.AddComponent<ResultsPanelView>();
            view.ConfigurePanelRoot(root);
            view.headingText = FlowPanelBuilder.AddHeading(card, "Track Complete");
            view.scoreText = FlowPanelBuilder.AddValue(card, "Total Score");
            view.maxComboText = FlowPanelBuilder.AddValue(card, "Max Combo");
            view.perfectText = FlowPanelBuilder.AddValue(card, "Perfect");
            view.goodText = FlowPanelBuilder.AddValue(card, "Good");
            view.missText = FlowPanelBuilder.AddValue(card, "Miss");
            view.gameModeText = FlowPanelBuilder.AddValue(card, "Game Mode");
            view.healthText = FlowPanelBuilder.AddValue(card, "Remaining Health");
            view.failureText = FlowPanelBuilder.AddCenteredText(
                card,
                "Failure",
                18,
                PulseForgeUITheme.Miss,
                42f);
            view.settingsText = FlowPanelBuilder.AddCenteredText(card, string.Empty, 20, PulseForgeUITheme.SecondaryText, 70f);
            view.replayButton = FlowPanelBuilder.AddButton(card, "Replay", PulseForgeUITheme.Primary, 72f, 28);
            view.anotherSongButton = FlowPanelBuilder.AddButton(card, "Choose Another Song", PulseForgeUITheme.SecondaryText);
            return view;
        }

        public void EnsureOutcomeFields(Action<GameObject> registerCreated = null)
        {
            Transform card = PanelRoot == null ? null : PanelRoot.transform.Find("Results Panel Card");
            if (card == null) return;
            Transform heading = card.Find("Heading");
            headingText = heading == null ? null : heading.GetComponent<Text>();
            gameModeText = EnsureValue(card, "Game Mode", registerCreated);
            healthText = EnsureValue(card, "Remaining Health", registerCreated);
            Transform failure = card.Find("Failure");
            if (failure == null)
            {
                failureText = FlowPanelBuilder.AddCenteredText(
                    card as RectTransform,
                    "Failure",
                    18,
                    PulseForgeUITheme.Miss,
                    42f);
                registerCreated?.Invoke(failureText.gameObject);
            }
            else
            {
                failureText = failure.GetComponent<Text>();
            }

            int insertIndex = missText == null ? card.childCount : missText.transform.GetSiblingIndex() + 1;
            gameModeText.transform.SetSiblingIndex(insertIndex++);
            healthText.transform.SetSiblingIndex(insertIndex++);
            failureText.transform.SetSiblingIndex(insertIndex);
            RectTransform cardRect = card as RectTransform;
            if (cardRect != null && cardRect.sizeDelta.y < 950f)
            {
                cardRect.sizeDelta = new Vector2(cardRect.sizeDelta.x, 950f);
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

            PulseForgeUIFactory.BindButton(replayButton, controller.RestartSession);
            PulseForgeUIFactory.BindButton(anotherSongButton, controller.ChooseAnotherSong);
        }

        public void Unbind()
        {
            PulseForgeUIFactory.UnbindButton(replayButton);
            PulseForgeUIFactory.UnbindButton(anotherSongButton);
            boundController = null;
        }

        public void Refresh(DebugRhythmPrototypeController controller)
        {
            ScoreSnapshot snapshot = controller.Score;
            headingText.text = controller.IsFailed ? "Run Failed" : "Track Complete";
            scoreText.text = "TOTAL SCORE\n" + snapshot.TotalScore;
            maxComboText.text = "MAX COMBO     " + snapshot.MaxCombo;
            perfectText.text = "PERFECT     " + snapshot.PerfectCount;
            perfectText.color = PulseForgeUITheme.Perfect;
            goodText.text = "GOOD     " + snapshot.GoodCount;
            goodText.color = PulseForgeUITheme.Good;
            missText.text = "MISS     " + snapshot.MissCount;
            missText.color = PulseForgeUITheme.Miss;
            gameModeText.text = "GAME MODE     " + controller.RunGameModeLabel;
            healthText.gameObject.SetActive(controller.RunGameMode == RadialGameMode.Survival);
            healthText.text = "REMAINING HEALTH     " + controller.RunHealth;
            failureText.gameObject.SetActive(controller.IsFailed);
            failureText.text = controller.IsFailed
                ? "FAILED AT " + FormatTime(controller.RunFailureTimeSeconds)
                    + "   •   " + controller.RunFailureReason
                : string.Empty;
            settingsText.text = controller.AppliedDifficultyLabel + " Difficulty   •   "
                + controller.AppliedCombatStyleLabel + " Combat Style";
        }

        public override void CollectValidationErrors(List<string> errors)
        {
            base.CollectValidationErrors(errors);
            PulseForgeUIValidation.AddMissing(errors, scoreText, "Results: score text is missing.");
            PulseForgeUIValidation.AddMissing(errors, headingText, "Results: outcome heading is missing.");
            PulseForgeUIValidation.AddMissing(errors, maxComboText, "Results: max combo text is missing.");
            PulseForgeUIValidation.AddMissing(errors, perfectText, "Results: Perfect text is missing.");
            PulseForgeUIValidation.AddMissing(errors, goodText, "Results: Good text is missing.");
            PulseForgeUIValidation.AddMissing(errors, missText, "Results: Miss text is missing.");
            PulseForgeUIValidation.AddMissing(errors, settingsText, "Results: settings text is missing.");
            PulseForgeUIValidation.AddMissing(errors, gameModeText, "Results: game mode text is missing.");
            PulseForgeUIValidation.AddMissing(errors, healthText, "Results: health text is missing.");
            PulseForgeUIValidation.AddMissing(errors, failureText, "Results: failure text is missing.");
            PulseForgeUIValidation.AddMissing(errors, replayButton, "Results: Replay button is missing.");
            PulseForgeUIValidation.AddMissing(errors, anotherSongButton, "Results: Choose Another Song button is missing.");
        }

        private static Text EnsureValue(
            Transform card,
            string name,
            Action<GameObject> registerCreated)
        {
            Transform existing = card.Find(name);
            if (existing != null) return existing.GetComponent<Text>();
            Text value = FlowPanelBuilder.AddValue(card as RectTransform, name);
            registerCreated?.Invoke(value.gameObject);
            return value;
        }

        private static string FormatTime(double seconds)
        {
            TimeSpan value = TimeSpan.FromSeconds(Math.Max(0d, seconds));
            return value.Minutes.ToString("00") + ":" + value.Seconds.ToString("00")
                + "." + value.Milliseconds.ToString("000");
        }
    }
}
