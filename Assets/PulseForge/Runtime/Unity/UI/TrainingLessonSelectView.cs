using System.Collections.Generic;
using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.Onboarding;
using PulseForge.Runtime.Unity.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class TrainingLessonSelectView : PulseForgePanelView
    {
        [SerializeField] private Text heading;
        [SerializeField] private Text basicHeading;
        [SerializeField] private Text advancedHeading;
        [SerializeField] private Button[] lessonButtons;
        [SerializeField] private Button exitButton;

        private DebugRhythmPrototypeController controller;

        public static TrainingLessonSelectView Create(Transform parent)
        {
            RectTransform card = PulseForgeM9HViewBuilder.CreateCard(
                "Training Lesson Select",
                parent,
                new Vector2(940f, 900f),
                out GameObject root);
            TrainingLessonSelectView view = root.AddComponent<TrainingLessonSelectView>();
            view.ConfigurePanelRoot(root);
            VerticalLayoutGroup cardLayout = card.GetComponent<VerticalLayoutGroup>();
            cardLayout.padding = new RectOffset(42, 42, 30, 30);
            cardLayout.spacing = 7f;
            view.heading = FlowPanelBuilder.AddHeading(card, "Training");
            view.basicHeading = FlowPanelBuilder.AddCenteredText(
                card, "Basic Lessons", 21, PulseForgeUITheme.Primary, 36f);
            IReadOnlyList<TrainingLessonDefinition> lessons = RadialTrainingCatalog.All;
            view.lessonButtons = new Button[lessons.Count];
            for (int i = 0; i < lessons.Count; i++)
            {
                if (i == 5)
                {
                    view.advancedHeading = FlowPanelBuilder.AddCenteredText(
                        card, "Advanced Lessons", 21, PulseForgeUITheme.Primary, 36f);
                }
                view.lessonButtons[i] = FlowPanelBuilder.AddButton(
                    card,
                    lessons[i].Id.ToString(),
                    lessons[i].Advanced ? PulseForgeUITheme.SecondaryText : PulseForgeUITheme.Primary,
                    39f,
                    17);
            }
            view.exitButton = FlowPanelBuilder.AddButton(
                card, "Exit Training", PulseForgeUITheme.SecondaryText, 48f, 17);
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
            if (controller == null || lessonButtons == null) return;
            for (int i = 0; i < lessonButtons.Length; i++)
            {
                int lessonIndex = i;
                PulseForgeUIFactory.BindButton(
                    lessonButtons[i],
                    () => controller.Experience?.StartLesson((TrainingLessonId)lessonIndex));
            }
            PulseForgeUIFactory.BindButton(exitButton, () => controller.Experience?.ExitTraining());
        }

        public void Unbind()
        {
            if (lessonButtons != null)
            {
                for (int i = 0; i < lessonButtons.Length; i++)
                {
                    PulseForgeUIFactory.UnbindButton(lessonButtons[i]);
                }
            }
            PulseForgeUIFactory.UnbindButton(exitButton);
            controller = null;
        }

        public void Refresh(DebugRhythmPrototypeController value)
        {
            PulseForgeExperienceCoordinator experience = value?.Experience;
            if (experience == null) return;
            heading.text = experience.Localize("Training");
            basicHeading.text = experience.Localize("BasicLessons");
            advancedHeading.text = experience.Localize("AdvancedLessons");
            for (int i = 0; i < lessonButtons.Length; i++)
            {
                TrainingLessonId id = (TrainingLessonId)i;
                string prefix = experience.IsLessonCompleted(id) ? "[Done] " : string.Empty;
                PulseForgeM9HViewBuilder.SetButtonLabel(
                    lessonButtons[i],
                    prefix + PulseForgeM9HLocalization.LessonName(id, experience.Language));
            }
            PulseForgeM9HViewBuilder.SetButtonLabel(exitButton, experience.Localize("ExitTraining"));
        }

        public override void CollectValidationErrors(List<string> errors)
        {
            base.CollectValidationErrors(errors);
            PulseForgeUIValidation.AddArray(errors, lessonButtons, 11, "M9H Training: lesson buttons are incomplete.");
        }
    }
}
