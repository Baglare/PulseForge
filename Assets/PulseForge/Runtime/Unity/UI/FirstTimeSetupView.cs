using System.Collections.Generic;
using PulseForge.Runtime.Unity.Onboarding;
using PulseForge.Runtime.Unity.Persistence;
using PulseForge.Runtime.Unity.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class FirstTimeSetupView : PulseForgePanelView
    {
        [SerializeField] private Text heading;
        [SerializeField] private Text stepLabel;
        [SerializeField] private Text description;
        [SerializeField] private Text detail;
        [SerializeField] private Button choiceOne;
        [SerializeField] private Button choiceTwo;
        [SerializeField] private Button choiceThree;
        [SerializeField] private Button backButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button skipButton;

        private DebugRhythmPrototypeController controller;

        public static FirstTimeSetupView Create(Transform parent)
        {
            RectTransform card = PulseForgeM9HViewBuilder.CreateCard(
                "First-Time Setup",
                parent,
                new Vector2(980f, 780f),
                out GameObject root);
            FirstTimeSetupView view = root.AddComponent<FirstTimeSetupView>();
            view.ConfigurePanelRoot(root);
            view.heading = FlowPanelBuilder.AddHeading(card, "First-Time Setup");
            view.stepLabel = FlowPanelBuilder.AddCenteredText(
                card, string.Empty, 26, PulseForgeUITheme.Primary, 48f);
            view.description = FlowPanelBuilder.AddCenteredText(
                card, string.Empty, 20, PulseForgeUITheme.PrimaryText, 92f);
            view.detail = FlowPanelBuilder.AddCenteredText(
                card, string.Empty, 18, PulseForgeUITheme.SecondaryText, 190f);
            RectTransform choices = PulseForgeM9HViewBuilder.AddButtonRow(card, 62f);
            view.choiceOne = PulseForgeUIFactory.CreateButton(
                "Choice One", choices, string.Empty, PulseForgeUITheme.SecondaryText, 18);
            view.choiceTwo = PulseForgeUIFactory.CreateButton(
                "Choice Two", choices, string.Empty, PulseForgeUITheme.Primary, 18);
            view.choiceThree = PulseForgeUIFactory.CreateButton(
                "Choice Three", choices, string.Empty, PulseForgeUITheme.SecondaryText, 18);
            RectTransform navigation = PulseForgeM9HViewBuilder.AddButtonRow(card, 60f);
            view.backButton = PulseForgeUIFactory.CreateButton(
                "Back", navigation, "Back", PulseForgeUITheme.SecondaryText, 18);
            view.skipButton = PulseForgeUIFactory.CreateButton(
                "Skip Setup", navigation, "Skip Setup", PulseForgeUITheme.SecondaryText, 18);
            view.nextButton = PulseForgeUIFactory.CreateButton(
                "Next", navigation, "Next", PulseForgeUITheme.Primary, 18);
            return view;
        }

        public void Bind(DebugRhythmPrototypeController value)
        {
            if (controller == value) return;
            Unbind();
            controller = value;
            if (controller == null) return;
            PulseForgeUIFactory.BindButton(choiceOne, () => SelectChoice(0));
            PulseForgeUIFactory.BindButton(choiceTwo, () => SelectChoice(1));
            PulseForgeUIFactory.BindButton(choiceThree, () => SelectChoice(2));
            PulseForgeUIFactory.BindButton(backButton, () => controller.Experience?.PreviousFirstTimeStep());
            PulseForgeUIFactory.BindButton(skipButton, () => controller.Experience?.RequestSkipFirstTimeSetup());
            PulseForgeUIFactory.BindButton(nextButton, Next);
        }

        public void Unbind()
        {
            PulseForgeUIFactory.UnbindButton(choiceOne);
            PulseForgeUIFactory.UnbindButton(choiceTwo);
            PulseForgeUIFactory.UnbindButton(choiceThree);
            PulseForgeUIFactory.UnbindButton(backButton);
            PulseForgeUIFactory.UnbindButton(skipButton);
            PulseForgeUIFactory.UnbindButton(nextButton);
            controller = null;
        }

        public void Refresh(DebugRhythmPrototypeController value)
        {
            PulseForgeExperienceCoordinator experience = value?.Experience;
            if (experience == null) return;
            heading.text = experience.Localize("FirstTimeSetup");
            if (experience.SkipConfirmationVisible)
            {
                stepLabel.text = experience.Localize("SkipSetup");
                description.text = experience.Localize("SkipWarning");
                detail.text = string.Empty;
                SetChoices(false, false, false);
                skipButton.gameObject.SetActive(false);
                backButton.gameObject.SetActive(true);
                PulseForgeM9HViewBuilder.SetButtonLabel(backButton, experience.Localize("Cancel"));
                PulseForgeM9HViewBuilder.SetButtonLabel(nextButton, experience.Localize("ConfirmSkip"));
                return;
            }

            skipButton.gameObject.SetActive(true);
            backButton.gameObject.SetActive(experience.FirstTimeStep != FirstTimeSetupStep.Language);
            PulseForgeM9HViewBuilder.SetButtonLabel(backButton, experience.Localize("Back"));
            PulseForgeM9HViewBuilder.SetButtonLabel(skipButton, experience.Localize("SkipSetup"));
            detail.text = string.Empty;
            switch (experience.FirstTimeStep)
            {
                case FirstTimeSetupStep.Language:
                    SetStep(experience, "Language", "LanguageDescription", "Next");
                    SetChoices(true, true, false);
                    PulseForgeM9HViewBuilder.SetButtonLabel(choiceOne, "English");
                    PulseForgeM9HViewBuilder.SetButtonLabel(choiceTwo, "Türkçe");
                    break;
                case FirstTimeSetupStep.ReadabilityProfile:
                    SetStep(experience, "ReadabilityProfile", "ReadabilityDescription", "Next");
                    SetChoices(true, true, true);
                    PulseForgeM9HViewBuilder.SetButtonLabel(choiceOne, experience.Localize("Standard"));
                    PulseForgeM9HViewBuilder.SetButtonLabel(choiceTwo, experience.Localize("Assisted"));
                    PulseForgeM9HViewBuilder.SetButtonLabel(choiceThree, experience.Localize("HighClarity"));
                    break;
                case FirstTimeSetupStep.BindingSummary:
                    SetStep(experience, "BindingSummary", "BindingsDescription", "Next");
                    SetChoices(false, false, false);
                    detail.text = experience.BindingSummary;
                    break;
                case FirstTimeSetupStep.Calibration:
                    SetStep(experience, "Calibration", "CalibrationDescription", "StartCalibration");
                    SetChoices(false, false, false);
                    break;
                case FirstTimeSetupStep.BasicTraining:
                    SetStep(experience, "BasicTraining", "BasicTrainingDescription", "StartBasicTraining");
                    SetChoices(false, false, false);
                    break;
                default:
                    SetStep(experience, "Complete", "CompleteDescription", "CompleteSetup");
                    SetChoices(false, false, false);
                    skipButton.gameObject.SetActive(false);
                    break;
            }
        }

        public override void CollectValidationErrors(List<string> errors)
        {
            base.CollectValidationErrors(errors);
            PulseForgeUIValidation.AddMissing(errors, nextButton, "M9H First-Time Setup: Next button is missing.");
            PulseForgeUIValidation.AddMissing(errors, skipButton, "M9H First-Time Setup: Skip button is missing.");
        }

        private void SelectChoice(int index)
        {
            PulseForgeExperienceCoordinator experience = controller?.Experience;
            if (experience == null) return;
            if (experience.FirstTimeStep == FirstTimeSetupStep.Language)
            {
                experience.SelectLanguage(index == 1
                    ? PulseForgeUILanguage.Turkish
                    : PulseForgeUILanguage.English);
            }
            else if (experience.FirstTimeStep == FirstTimeSetupStep.ReadabilityProfile)
            {
                experience.SelectReadabilityProfile((PulseForgeReadabilityProfile)index);
            }
        }

        private void Next()
        {
            PulseForgeExperienceCoordinator experience = controller?.Experience;
            if (experience == null) return;
            if (experience.SkipConfirmationVisible)
            {
                experience.ConfirmSkipFirstTimeSetup();
            }
            else
            {
                experience.NextFirstTimeStep();
            }
        }

        private void SetStep(
            PulseForgeExperienceCoordinator experience,
            string titleKey,
            string descriptionKey,
            string nextKey)
        {
            stepLabel.text = experience.Localize(titleKey);
            description.text = experience.Localize(descriptionKey);
            PulseForgeM9HViewBuilder.SetButtonLabel(nextButton, experience.Localize(nextKey));
        }

        private void SetChoices(bool one, bool two, bool three)
        {
            choiceOne.gameObject.SetActive(one);
            choiceTwo.gameObject.SetActive(two);
            choiceThree.gameObject.SetActive(three);
        }
    }
}
