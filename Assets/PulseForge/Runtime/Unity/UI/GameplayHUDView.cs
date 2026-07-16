using System.Collections.Generic;
using System;
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
        [SerializeField] private RectTransform runStatusRoot;
        [SerializeField] private Text gameModeText;
        [SerializeField] private GameObject healthRoot;
        [SerializeField] private RectTransform healthFill;
        [SerializeField] private Text healthText;
        [SerializeField] private Text oneLifeText;

        private DebugRhythmPrototypeController boundController;
        private bool gameplayFeedbackManaged;
        private int lastDamageRevision = -1;
        private float damageFlashUntil;

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
            view.EnsureGameModeHud();
            return view;
        }

        public void EnsureGameModeHud(Action<GameObject> registerCreated = null)
        {
            Transform root = PanelRoot == null ? null : PanelRoot.transform;
            if (root == null) return;
            Transform existing = root.Find("Run Status");
            if (existing != null)
            {
                runStatusRoot = existing as RectTransform;
                gameModeText = existing.Find("Game Mode")?.GetComponent<Text>();
                Transform existingHealth = existing.Find("Health");
                healthRoot = existingHealth == null ? null : existingHealth.gameObject;
                healthFill = existingHealth == null ? null : existingHealth.Find("Fill") as RectTransform;
                healthText = existingHealth == null ? null : existingHealth.Find("Value")?.GetComponent<Text>();
                oneLifeText = existing.Find("One Life")?.GetComponent<Text>();
                return;
            }

            runStatusRoot = PulseForgeUIFactory.CreatePanel(
                "Run Status",
                root,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Surface, 0.92f));
            PulseForgeUIFactory.SetTop(runStatusRoot, 82f, 1390f, 24f, 148f);
            gameModeText = PulseForgeUIFactory.CreateText(
                "Game Mode",
                runStatusRoot,
                "STANDARD",
                16,
                PulseForgeUITheme.SecondaryText,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            gameModeText.rectTransform.anchorMin = new Vector2(0f, 0.52f);
            gameModeText.rectTransform.anchorMax = Vector2.one;
            gameModeText.rectTransform.offsetMin = Vector2.zero;
            gameModeText.rectTransform.offsetMax = Vector2.zero;

            RectTransform health = PulseForgeUIFactory.CreateRect("Health", runStatusRoot);
            health.anchorMin = new Vector2(0.08f, 0.14f);
            health.anchorMax = new Vector2(0.92f, 0.47f);
            health.offsetMin = Vector2.zero;
            health.offsetMax = Vector2.zero;
            healthRoot = health.gameObject;
            Image background = health.gameObject.AddComponent<Image>();
            background.color = PulseForgeUITheme.SurfaceSoft;
            healthFill = PulseForgeUIFactory.CreateRect("Fill", health);
            healthFill.anchorMin = Vector2.zero;
            healthFill.anchorMax = Vector2.one;
            healthFill.offsetMin = Vector2.zero;
            healthFill.offsetMax = Vector2.zero;
            healthFill.gameObject.AddComponent<Image>().color = PulseForgeUITheme.Good;
            healthText = PulseForgeUIFactory.CreateText(
                "Value", health, "100", 15, PulseForgeUITheme.PrimaryText,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            oneLifeText = PulseForgeUIFactory.CreateText(
                "One Life", runStatusRoot, "ONE LIFE", 19, PulseForgeUITheme.Primary,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            oneLifeText.rectTransform.anchorMin = new Vector2(0f, 0f);
            oneLifeText.rectTransform.anchorMax = new Vector2(1f, 0.52f);
            oneLifeText.rectTransform.offsetMin = Vector2.zero;
            oneLifeText.rectTransform.offsetMax = Vector2.zero;
            registerCreated?.Invoke(runStatusRoot.gameObject);
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
            lastDamageRevision = -1;
            damageFlashUntil = 0f;
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

        public void SetRhythmLaneVisible(bool visible)
        {
            if (rhythmLaneView != null && rhythmLaneView.gameObject.activeSelf != visible)
            {
                rhythmLaneView.gameObject.SetActive(visible);
            }
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
            RefreshRunStatus(controller);

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

            if (rhythmLaneView != null && rhythmLaneView.gameObject.activeSelf)
            {
                rhythmLaneView.Refresh(controller.SessionEvents, controller.CurrentSongTimeSeconds);
            }
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
            PulseForgeUIValidation.AddMissing(errors, runStatusRoot, "Gameplay HUD: run status is missing.");
            PulseForgeUIValidation.AddMissing(errors, gameModeText, "Gameplay HUD: game mode text is missing.");
            PulseForgeUIValidation.AddMissing(errors, healthRoot, "Gameplay HUD: health root is missing.");
            PulseForgeUIValidation.AddMissing(errors, healthFill, "Gameplay HUD: health fill is missing.");
            PulseForgeUIValidation.AddMissing(errors, healthText, "Gameplay HUD: health value is missing.");
            PulseForgeUIValidation.AddMissing(errors, oneLifeText, "Gameplay HUD: One Life indicator is missing.");
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

        private void RefreshRunStatus(DebugRhythmPrototypeController controller)
        {
            gameModeText.text = controller.RunGameModeLabel.ToUpperInvariant();
            bool survival = controller.RunGameMode == RadialGameMode.Survival;
            bool oneLife = controller.RunGameMode == RadialGameMode.OneLife;
            healthRoot.SetActive(survival);
            oneLifeText.gameObject.SetActive(oneLife);
            if (lastDamageRevision >= 0 && controller.RunDamageRevision > lastDamageRevision)
            {
                damageFlashUntil = Time.unscaledTime + 0.22f;
            }
            lastDamageRevision = controller.RunDamageRevision;

            if (survival)
            {
                float health = Mathf.Clamp01(controller.RunHealth / 100f);
                healthFill.anchorMax = new Vector2(health, 1f);
                healthFill.offsetMax = Vector2.zero;
                healthText.text = controller.RunHealth.ToString();
                Image fillImage = healthFill.GetComponent<Image>();
                fillImage.color = controller.IsFailed || Time.unscaledTime < damageFlashUntil
                    ? PulseForgeUITheme.Miss
                    : PulseForgeUITheme.Good;
            }
            if (oneLife)
            {
                oneLifeText.text = controller.IsFailed ? "ONE LIFE  ✕" : "ONE LIFE";
                oneLifeText.color = controller.IsFailed
                    ? PulseForgeUITheme.Miss
                    : PulseForgeUITheme.Primary;
            }
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
