using System.Collections.Generic;
using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class GameplayHUDView : PulseForgePanelView
    {
        [SerializeField] private Text scoreText;
        [SerializeField] private Text comboText;
        [SerializeField] private Text songText;
        [SerializeField] private RectTransform progressFill;
        [SerializeField] private Text feedbackText;
        [SerializeField] private Button pauseButton;
        [SerializeField] private RhythmLaneView rhythmLaneView;

        private DebugRhythmPrototypeController boundController;
        private bool gameplayFeedbackManaged;

        public RhythmLaneView RhythmLaneView => rhythmLaneView;
        public Text ComboText => comboText;
        public Text FeedbackText => feedbackText;

        public static GameplayHUDView Create(Transform parent)
        {
            RectTransform rootRect = PulseForgeUIFactory.CreateStretchRect("Gameplay HUD", parent);
            GameplayHUDView view = rootRect.gameObject.AddComponent<GameplayHUDView>();
            view.ConfigurePanelRoot(rootRect.gameObject);

            RectTransform topBar = PulseForgeUIFactory.CreatePanel(
                "Top HUD",
                rootRect,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Surface, 0.92f));
            PulseForgeUIFactory.SetTop(topBar, 116f, 24f, 24f, 20f);

            view.scoreText = CreateTopMetric(topBar, "Score", new Vector2(0.02f, 0.12f), new Vector2(0.18f, 0.90f));
            view.comboText = CreateTopMetric(topBar, "Combo", new Vector2(0.18f, 0.12f), new Vector2(0.34f, 0.90f));
            view.songText = CreateTopMetric(topBar, string.Empty, new Vector2(0.35f, 0.34f), new Vector2(0.80f, 0.95f));
            view.songText.alignment = TextAnchor.MiddleCenter;
            view.songText.fontSize = 28;

            RectTransform progressBackground = PulseForgeUIFactory.CreateRect("Song Progress", topBar);
            progressBackground.anchorMin = new Vector2(0.37f, 0.14f);
            progressBackground.anchorMax = new Vector2(0.78f, 0.25f);
            progressBackground.offsetMin = Vector2.zero;
            progressBackground.offsetMax = Vector2.zero;
            Image progressBackgroundImage = progressBackground.gameObject.AddComponent<Image>();
            progressBackgroundImage.color = PulseForgeUITheme.SurfaceSoft;

            view.progressFill = PulseForgeUIFactory.CreateRect("Fill", progressBackground);
            view.progressFill.anchorMin = Vector2.zero;
            view.progressFill.anchorMax = new Vector2(0f, 1f);
            view.progressFill.offsetMin = Vector2.zero;
            view.progressFill.offsetMax = Vector2.zero;
            Image progressFillImage = view.progressFill.gameObject.AddComponent<Image>();
            progressFillImage.color = PulseForgeUITheme.Primary;

            view.pauseButton = PulseForgeUIFactory.CreateButton("Pause", topBar, "Pause", PulseForgeUITheme.Primary, 22);
            RectTransform pauseRect = view.pauseButton.GetComponent<RectTransform>();
            pauseRect.anchorMin = new Vector2(0.84f, 0.22f);
            pauseRect.anchorMax = new Vector2(0.98f, 0.78f);
            pauseRect.offsetMin = Vector2.zero;
            pauseRect.offsetMax = Vector2.zero;

            RectTransform centerFeedback = PulseForgeUIFactory.CreateStretchRect("Center Feedback", rootRect);
            view.feedbackText = PulseForgeUIFactory.CreateText(
                "Combat Feedback",
                centerFeedback,
                string.Empty,
                54,
                PulseForgeUITheme.Perfect,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            view.feedbackText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            view.feedbackText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            view.feedbackText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            view.feedbackText.rectTransform.sizeDelta = new Vector2(980f, 120f);
            view.feedbackText.rectTransform.anchoredPosition = new Vector2(0f, 130f);

            RectTransform bottomHud = PulseForgeUIFactory.CreatePanel(
                "Rhythm Lanes",
                rootRect,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Surface, 0.90f));
            PulseForgeUIFactory.SetBottom(bottomHud, 270f, 24f, 24f, 20f);
            view.rhythmLaneView = RhythmLaneView.Create(bottomHud);
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

            PulseForgeUIFactory.BindButton(pauseButton, controller.PauseSession);
            rhythmLaneView.InitializeRuntimePool();
        }

        public void Unbind()
        {
            PulseForgeUIFactory.UnbindButton(pauseButton);
            SetGameplayFeedbackManaged(false);
            boundController = null;
        }

        public void SetGameplayFeedbackManaged(bool isManaged)
        {
            gameplayFeedbackManaged = isManaged;
        }

        public void Refresh(DebugRhythmPrototypeController controller)
        {
            ScoreSnapshot snapshot = controller.Score;
            scoreText.text = "<size=13><color=#8FA3B8>SCORE</color></size>\n" + snapshot.TotalScore;
            comboText.text = "<size=13><color=#8FA3B8>COMBO</color></size>\n"
                + (snapshot.CurrentCombo == 0
                    ? "<color=#8FA3B8>0</color>"
                    : snapshot.CurrentCombo.ToString());
            songText.text = controller.SongName;

            float progress = controller.SessionDurationSeconds <= 0d
                ? 0f
                : Mathf.Clamp01((float)(controller.CurrentSongTimeSeconds / controller.SessionDurationSeconds));
            progressFill.anchorMax = new Vector2(progress, 1f);
            progressFill.offsetMax = Vector2.zero;
            pauseButton.interactable = controller.CanPause;

            if (!gameplayFeedbackManaged)
            {
                PulseForgeFeedbackPresentation feedback = controller.CurrentFeedback;
                feedbackText.gameObject.SetActive(feedback.IsVisible);
                if (feedback.IsVisible)
                {
                    feedbackText.text = feedback.Text;
                    Color color = GetFeedbackColor(feedback);
                    color.a = feedback.Alpha;
                    feedbackText.color = color;
                }
            }

            rhythmLaneView.Refresh(controller.SessionEvents, controller.CurrentSongTimeSeconds);
        }

        public override void CollectValidationErrors(List<string> errors)
        {
            base.CollectValidationErrors(errors);
            PulseForgeUIValidation.AddMissing(errors, scoreText, "Gameplay HUD: score text is missing.");
            PulseForgeUIValidation.AddMissing(errors, comboText, "Gameplay HUD: combo text is missing.");
            PulseForgeUIValidation.AddMissing(errors, songText, "Gameplay HUD: song text is missing.");
            PulseForgeUIValidation.AddMissing(errors, progressFill, "Gameplay HUD: progress fill is missing.");
            PulseForgeUIValidation.AddMissing(errors, feedbackText, "Gameplay HUD: feedback text is missing.");
            PulseForgeUIValidation.AddMissing(errors, pauseButton, "Gameplay HUD: Pause button is missing.");
            PulseForgeUIValidation.AddMissing(errors, rhythmLaneView, "Gameplay HUD: RhythmLaneView is missing.");
            rhythmLaneView?.CollectValidationErrors(errors);
        }

        private static Text CreateTopMetric(Transform parent, string value, Vector2 anchorMin, Vector2 anchorMax)
        {
            Text text = PulseForgeUIFactory.CreateText(
                value,
                parent,
                value,
                24,
                PulseForgeUITheme.PrimaryText,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            text.rectTransform.anchorMin = anchorMin;
            text.rectTransform.anchorMax = anchorMax;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return text;
        }

        private static Color GetFeedbackColor(PulseForgeFeedbackPresentation feedback)
        {
            if (feedback.Grade == HitGrade.Miss)
            {
                return PulseForgeUITheme.Miss;
            }

            if (feedback.Grade == HitGrade.Perfect)
            {
                return PulseForgeUITheme.Perfect;
            }

            if (feedback.Grade == HitGrade.Good)
            {
                return PulseForgeUITheme.Good;
            }

            return feedback.Action == RhythmAction.Guard ? PulseForgeUITheme.Guard : PulseForgeUITheme.Strike;
        }
    }
}
