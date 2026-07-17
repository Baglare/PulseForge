using System.Collections.Generic;
using PulseForge.Runtime.Unity.Onboarding;
using PulseForge.Runtime.Unity.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class TrainingResultView : PulseForgePanelView
    {
        [SerializeField] private Text heading;
        [SerializeField] private Text lesson;
        [SerializeField] private Text attempts;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button exitButton;

        private DebugRhythmPrototypeController controller;
        private int lastHandledButtonFrame = -1;

        public static TrainingResultView Create(Transform parent)
        {
            RectTransform card = PulseForgeM9HViewBuilder.CreateCard(
                "Training Result",
                parent,
                new Vector2(760f, 500f),
                out GameObject root);
            TrainingResultView view = root.AddComponent<TrainingResultView>();
            view.ConfigurePanelRoot(root);
            view.heading = FlowPanelBuilder.AddHeading(card, "Lesson Complete");
            view.lesson = FlowPanelBuilder.AddCenteredText(
                card, string.Empty, 28, PulseForgeUITheme.Primary, 80f);
            view.attempts = FlowPanelBuilder.AddCenteredText(
                card, string.Empty, 20, PulseForgeUITheme.PrimaryText, 64f);
            RectTransform actions = PulseForgeM9HViewBuilder.AddButtonRow(card, 60f);
            view.retryButton = PulseForgeUIFactory.CreateButton(
                "Retry", actions, "Retry", PulseForgeUITheme.SecondaryText, 17);
            view.nextButton = PulseForgeUIFactory.CreateButton(
                "Next Lesson", actions, "Next Lesson", PulseForgeUITheme.Primary, 17);
            view.exitButton = PulseForgeUIFactory.CreateButton(
                "Exit Training", actions, "Exit Training", PulseForgeUITheme.SecondaryText, 17);
            return view;
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
            PulseForgeUIFactory.BindButton(nextButton, HandleNext);
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
            else if (PulseForgeM9HViewBuilder.IsPointerReleasedOver(nextButton))
            {
                HandleNext();
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

        private void HandleNext()
        {
            if (lastHandledButtonFrame == Time.frameCount) return;
            lastHandledButtonFrame = Time.frameCount;
            controller?.Experience?.NextLesson();
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
            PulseForgeUIFactory.UnbindButton(nextButton);
            PulseForgeUIFactory.UnbindButton(exitButton);
            controller = null;
        }

        public void Refresh(DebugRhythmPrototypeController value)
        {
            PulseForgeExperienceCoordinator experience = value?.Experience;
            if (experience == null) return;
            heading.text = experience.Localize("LessonComplete");
            lesson.text = experience.CurrentLessonName;
            attempts.text = experience.Localize("SuccessfulAttempts") + ": "
                + experience.SuccessfulTrainingAttempts + " / 2";
            PulseForgeM9HViewBuilder.SetButtonLabel(retryButton, experience.Localize("Retry"));
            PulseForgeM9HViewBuilder.SetButtonLabel(nextButton, experience.Localize("NextLesson"));
            PulseForgeM9HViewBuilder.SetButtonLabel(exitButton, experience.Localize("ExitTraining"));
        }

        public override void CollectValidationErrors(List<string> errors)
        {
            base.CollectValidationErrors(errors);
            PulseForgeUIValidation.AddMissing(errors, nextButton, "M9H Training Result: Next button is missing.");
        }
    }
}
