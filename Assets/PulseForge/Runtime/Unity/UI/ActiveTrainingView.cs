using System;
using System.Collections.Generic;
using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.Onboarding;
using PulseForge.Runtime.Unity.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class ActiveTrainingView : PulseForgePanelView
    {
        [SerializeField] private Text heading;
        [SerializeField] private Text description;
        [SerializeField] private Text bindings;
        [SerializeField] private Text prompt;
        [SerializeField] private Text feedback;
        [SerializeField] private Text attempts;
        [SerializeField] private Text timingLegend;
        [SerializeField] private TrainingTimingBarView timingBar;
        [SerializeField] private PulseForgeRingGraphic pulse;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button exitButton;

        private DebugRhythmPrototypeController controller;
        private int lastHandledButtonFrame = -1;

        public static ActiveTrainingView Create(Transform parent)
        {
            RectTransform card = PulseForgeM9HViewBuilder.CreateCard(
                "Active Training",
                parent,
                new Vector2(940f, 760f),
                out GameObject root);
            ActiveTrainingView view = root.AddComponent<ActiveTrainingView>();
            view.ConfigurePanelRoot(root);
            view.heading = FlowPanelBuilder.AddHeading(card, "Training");
            view.description = FlowPanelBuilder.AddCenteredText(
                card, string.Empty, 20, PulseForgeUITheme.PrimaryText, 72f);
            view.bindings = FlowPanelBuilder.AddCenteredText(
                card, string.Empty, 24, PulseForgeUITheme.Primary, 52f);
            view.prompt = FlowPanelBuilder.AddCenteredText(
                card, string.Empty, 21, PulseForgeUITheme.PrimaryText, 74f);
            RectTransform pulseRoot = PulseForgeUIFactory.CreateRect("Training Pulse", card);
            PulseForgeUIFactory.SetLayoutHeight(pulseRoot, 142f);
            pulseRoot.sizeDelta = new Vector2(112f, 112f);
            view.pulse = pulseRoot.gameObject.AddComponent<PulseForgeRingGraphic>();
            view.pulse.color = PulseForgeUITheme.Primary;
            view.feedback = FlowPanelBuilder.AddCenteredText(
                card, string.Empty, 26, PulseForgeUITheme.PrimaryText, 52f);
            view.attempts = FlowPanelBuilder.AddCenteredText(
                card, string.Empty, 18, PulseForgeUITheme.SecondaryText, 38f);
            view.timingLegend = FlowPanelBuilder.AddCenteredText(
                card, string.Empty, 17, PulseForgeUITheme.SecondaryText, 46f);
            RectTransform actions = PulseForgeM9HViewBuilder.AddButtonRow(card, 56f);
            view.retryButton = PulseForgeUIFactory.CreateButton(
                "Retry", actions, "Retry", PulseForgeUITheme.SecondaryText, 17);
            view.exitButton = PulseForgeUIFactory.CreateButton(
                "Exit Training", actions, "Exit Training", PulseForgeUITheme.SecondaryText, 17);
            view.EnsureTimingBar();
            return view;
        }

        public void EnsureTimingBar(Action<GameObject> registerCreated = null)
        {
            if (timingBar == null)
            {
                timingBar = GetComponentInChildren<TrainingTimingBarView>(true);
            }

            Transform card = prompt == null ? null : prompt.transform.parent;
            if (timingBar == null && card != null)
            {
                timingBar = TrainingTimingBarView.Create(card);
                timingBar.transform.SetSiblingIndex(prompt.transform.GetSiblingIndex());
                registerCreated?.Invoke(timingBar.gameObject);
            }

            VerticalLayoutGroup layout = card == null
                ? null
                : card.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = 10f;
            }
            SetCompactHeight(heading, 60f);
            SetCompactHeight(description, 54f);
            SetCompactHeight(bindings, 38f);
            SetCompactHeight(prompt, 54f);
            SetCompactHeight(pulse, 86f);
            SetCompactHeight(feedback, 40f);
            SetCompactHeight(attempts, 30f);
            SetCompactHeight(timingLegend, 36f);
            if (retryButton != null && retryButton.transform.parent != null)
            {
                SetCompactHeight(retryButton.transform.parent, 52f);
            }
        }

        public void Bind(DebugRhythmPrototypeController value)
        {
            if (controller == value)
            {
                BindButtons();
                return;
            }
            Unbind();
            controller = value;
            BindButtons();
        }

        private void OnEnable()
        {
            BindButtons();
        }

        private void BindButtons()
        {
            if (controller == null) return;
            PulseForgeUIFactory.BindButton(retryButton, HandleRetry);
            PulseForgeUIFactory.BindButton(exitButton, HandleExit);
        }

        private void LateUpdate()
        {
            if (controller == null || lastHandledButtonFrame == Time.frameCount)
            {
                return;
            }
            if (PulseForgeM9HViewBuilder.IsPointerReleasedOver(retryButton))
            {
                HandleRetry();
            }
            else if (PulseForgeM9HViewBuilder.IsPointerReleasedOver(exitButton))
            {
                HandleExit();
            }
        }

        private void HandleRetry()
        {
            if (lastHandledButtonFrame == Time.frameCount) return;
            lastHandledButtonFrame = Time.frameCount;
            controller?.Experience?.RetryLesson();
        }

        private void HandleExit()
        {
            if (lastHandledButtonFrame == Time.frameCount) return;
            lastHandledButtonFrame = Time.frameCount;
            controller?.Experience?.ExitTraining();
        }

        public void Unbind()
        {
            PulseForgeUIFactory.UnbindButton(retryButton);
            PulseForgeUIFactory.UnbindButton(exitButton);
            controller = null;
        }

        public void Refresh(DebugRhythmPrototypeController value)
        {
            PulseForgeExperienceCoordinator experience = value?.Experience;
            if (experience == null) return;
            heading.text = experience.CurrentLessonName;
            description.text = experience.CurrentLessonDescription;
            bindings.text = experience.CurrentLessonBindings;
            prompt.text = experience.TrainingPrompt;
            feedback.text = experience.TrainingFeedback;
            attempts.text = experience.Localize("SuccessfulAttempts") + ": "
                + experience.SuccessfulTrainingAttempts + " / 2";
            timingLegend.text = experience.CurrentLesson == TrainingLessonId.TimingBar
                ? experience.Localize("TimingBarLegend")
                : string.Empty;
            timingBar?.Refresh(experience);
            float scale = 0.72f + (experience.VisualPulse01 * 0.62f);
            pulse.rectTransform.localScale = new Vector3(scale, scale, 1f);
            PulseForgeM9HViewBuilder.SetButtonLabel(retryButton, experience.Localize("Retry"));
            PulseForgeM9HViewBuilder.SetButtonLabel(exitButton, experience.Localize("ExitTraining"));
        }

        public override void CollectValidationErrors(List<string> errors)
        {
            base.CollectValidationErrors(errors);
            PulseForgeUIValidation.AddMissing(errors, retryButton, "M9H Active Training: Retry button is missing.");
            PulseForgeUIValidation.AddMissing(errors, exitButton, "M9H Active Training: Exit button is missing.");
            PulseForgeUIValidation.AddMissing(errors, timingBar, "M9H Active Training: Timing Bar is missing.");
            timingBar?.CollectValidationErrors(errors);
        }

        private static void SetCompactHeight(Component component, float height)
        {
            if (component != null)
            {
                PulseForgeUIFactory.SetLayoutHeight(component, height);
            }
        }
    }
}
