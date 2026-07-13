using System.Collections.Generic;
using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class ResultsPanelView : PulseForgePanelView
    {
        [SerializeField] private Text scoreText;
        [SerializeField] private Text maxComboText;
        [SerializeField] private Text perfectText;
        [SerializeField] private Text goodText;
        [SerializeField] private Text missText;
        [SerializeField] private Text settingsText;
        [SerializeField] private Button replayButton;
        [SerializeField] private Button anotherSongButton;

        private DebugRhythmPrototypeController boundController;

        public static ResultsPanelView Create(Transform parent)
        {
            RectTransform card = FlowPanelBuilder.CreateScreenWithCard(
                "Results Panel",
                parent,
                new Vector2(840f, 830f),
                out GameObject root);
            ResultsPanelView view = root.AddComponent<ResultsPanelView>();
            view.ConfigurePanelRoot(root);
            FlowPanelBuilder.AddHeading(card, "Track Complete");
            view.scoreText = FlowPanelBuilder.AddValue(card, "Total Score");
            view.maxComboText = FlowPanelBuilder.AddValue(card, "Max Combo");
            view.perfectText = FlowPanelBuilder.AddValue(card, "Perfect");
            view.goodText = FlowPanelBuilder.AddValue(card, "Good");
            view.missText = FlowPanelBuilder.AddValue(card, "Miss");
            view.settingsText = FlowPanelBuilder.AddCenteredText(card, string.Empty, 20, PulseForgeUITheme.SecondaryText, 70f);
            view.replayButton = FlowPanelBuilder.AddButton(card, "Replay", PulseForgeUITheme.Primary, 72f, 28);
            view.anotherSongButton = FlowPanelBuilder.AddButton(card, "Choose Another Song", PulseForgeUITheme.SecondaryText);
            return view;
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
            scoreText.text = "TOTAL SCORE\n" + snapshot.TotalScore;
            maxComboText.text = "MAX COMBO     " + snapshot.MaxCombo;
            perfectText.text = "PERFECT     " + snapshot.PerfectCount;
            perfectText.color = PulseForgeUITheme.Perfect;
            goodText.text = "GOOD     " + snapshot.GoodCount;
            goodText.color = PulseForgeUITheme.Good;
            missText.text = "MISS     " + snapshot.MissCount;
            missText.color = PulseForgeUITheme.Miss;
            settingsText.text = controller.AppliedDifficultyLabel + " Difficulty   •   "
                + controller.AppliedCombatStyleLabel + " Combat Style";
        }

        public override void CollectValidationErrors(List<string> errors)
        {
            base.CollectValidationErrors(errors);
            PulseForgeUIValidation.AddMissing(errors, scoreText, "Results: score text is missing.");
            PulseForgeUIValidation.AddMissing(errors, maxComboText, "Results: max combo text is missing.");
            PulseForgeUIValidation.AddMissing(errors, perfectText, "Results: Perfect text is missing.");
            PulseForgeUIValidation.AddMissing(errors, goodText, "Results: Good text is missing.");
            PulseForgeUIValidation.AddMissing(errors, missText, "Results: Miss text is missing.");
            PulseForgeUIValidation.AddMissing(errors, settingsText, "Results: settings text is missing.");
            PulseForgeUIValidation.AddMissing(errors, replayButton, "Results: Replay button is missing.");
            PulseForgeUIValidation.AddMissing(errors, anotherSongButton, "Results: Choose Another Song button is missing.");
        }
    }
}
