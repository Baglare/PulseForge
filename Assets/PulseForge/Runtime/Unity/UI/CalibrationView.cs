using System.Collections.Generic;
using System.Globalization;
using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.Onboarding;
using PulseForge.Runtime.Unity.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class CalibrationView : PulseForgePanelView
    {
        [SerializeField] private Text heading;
        [SerializeField] private Text description;
        [SerializeField] private Text value;
        [SerializeField] private PulseForgeRingGraphic pulse;
        [SerializeField] private Button decreaseButton;
        [SerializeField] private Button increaseButton;
        [SerializeField] private Button primaryButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button keepButton;
        [SerializeField] private Button cancelButton;

        private DebugRhythmPrototypeController controller;

        public static CalibrationView Create(Transform parent)
        {
            RectTransform card = PulseForgeM9HViewBuilder.CreateCard(
                "Calibration",
                parent,
                new Vector2(900f, 720f),
                out GameObject root);
            CalibrationView view = root.AddComponent<CalibrationView>();
            view.ConfigurePanelRoot(root);
            view.heading = FlowPanelBuilder.AddHeading(card, "Calibration");
            view.description = FlowPanelBuilder.AddCenteredText(
                card, string.Empty, 20, PulseForgeUITheme.PrimaryText, 116f);
            RectTransform pulseRoot = PulseForgeUIFactory.CreateRect("Visual Pulse", card);
            PulseForgeUIFactory.SetLayoutHeight(pulseRoot, 150f);
            pulseRoot.anchorMin = new Vector2(0.5f, 0.5f);
            pulseRoot.anchorMax = new Vector2(0.5f, 0.5f);
            pulseRoot.sizeDelta = new Vector2(118f, 118f);
            view.pulse = pulseRoot.gameObject.AddComponent<PulseForgeRingGraphic>();
            view.pulse.color = PulseForgeUITheme.Primary;
            view.value = FlowPanelBuilder.AddCenteredText(
                card, string.Empty, 22, PulseForgeUITheme.PrimaryText, 122f);
            RectTransform adjust = PulseForgeM9HViewBuilder.AddButtonRow(card, 58f);
            view.decreaseButton = PulseForgeUIFactory.CreateButton(
                "Decrease 5 ms", adjust, "- 5 ms", PulseForgeUITheme.SecondaryText, 18);
            view.increaseButton = PulseForgeUIFactory.CreateButton(
                "Increase 5 ms", adjust, "+ 5 ms", PulseForgeUITheme.SecondaryText, 18);
            RectTransform actions = PulseForgeM9HViewBuilder.AddButtonRow(card, 58f);
            view.retryButton = PulseForgeUIFactory.CreateButton(
                "Retry", actions, "Retry", PulseForgeUITheme.SecondaryText, 17);
            view.keepButton = PulseForgeUIFactory.CreateButton(
                "Keep Current", actions, "Keep Current", PulseForgeUITheme.SecondaryText, 17);
            view.primaryButton = PulseForgeUIFactory.CreateButton(
                "Apply Suggested", actions, "Apply", PulseForgeUITheme.Primary, 17);
            view.cancelButton = FlowPanelBuilder.AddButton(
                card, "Cancel", PulseForgeUITheme.SecondaryText, 52f, 17);
            return view;
        }

        public void Bind(DebugRhythmPrototypeController valueController)
        {
            if (controller == valueController) return;
            Unbind();
            controller = valueController;
            if (controller == null) return;
            PulseForgeUIFactory.BindButton(decreaseButton, () => controller.Experience?.AdjustAudioVisualAlignment(-1));
            PulseForgeUIFactory.BindButton(increaseButton, () => controller.Experience?.AdjustAudioVisualAlignment(1));
            PulseForgeUIFactory.BindButton(primaryButton, Primary);
            PulseForgeUIFactory.BindButton(retryButton, Retry);
            PulseForgeUIFactory.BindButton(keepButton, KeepCurrent);
            PulseForgeUIFactory.BindButton(cancelButton, () => controller.Experience?.CancelCalibration());
        }

        public void Unbind()
        {
            PulseForgeUIFactory.UnbindButton(decreaseButton);
            PulseForgeUIFactory.UnbindButton(increaseButton);
            PulseForgeUIFactory.UnbindButton(primaryButton);
            PulseForgeUIFactory.UnbindButton(retryButton);
            PulseForgeUIFactory.UnbindButton(keepButton);
            PulseForgeUIFactory.UnbindButton(cancelButton);
            controller = null;
        }

        public void Refresh(DebugRhythmPrototypeController valueController)
        {
            PulseForgeExperienceCoordinator experience = valueController?.Experience;
            if (experience == null) return;
            float scale = 0.75f + (experience.VisualPulse01 * 0.55f);
            pulse.rectTransform.localScale = new Vector3(scale, scale, 1f);
            PulseForgeM9HViewBuilder.SetButtonLabel(cancelButton, experience.Localize("Cancel"));
            switch (experience.CalibrationStep)
            {
                case CalibrationStep.AudioVisualAlignment:
                    heading.text = experience.Localize("AudioVisualAlignment");
                    description.text = experience.Localize("AudioVisualDescription");
                    value.text = experience.CandidateBeatMapOffsetMilliseconds.ToString(
                        "+0;-0;0", CultureInfo.InvariantCulture) + " ms";
                    SetVisible(true, true, true, true);
                    primaryButton.interactable = true;
                    PulseForgeM9HViewBuilder.SetButtonLabel(primaryButton, experience.Localize("Apply"));
                    PulseForgeM9HViewBuilder.SetButtonLabel(retryButton, experience.Localize("Retry"));
                    PulseForgeM9HViewBuilder.SetButtonLabel(keepButton, experience.Localize("KeepCurrent"));
                    break;
                case CalibrationStep.InputInstructions:
                    heading.text = experience.Localize("InputTimingCalibration");
                    description.text = experience.Localize("InputCalibrationDescription");
                    value.text = "Guard: " + experience.GuardBinding;
                    SetVisible(false, true, false, false);
                    primaryButton.interactable = true;
                    PulseForgeM9HViewBuilder.SetButtonLabel(primaryButton, experience.Localize("StartMeasurement"));
                    break;
                case CalibrationStep.InputMeasuring:
                    heading.text = experience.Localize("Measuring");
                    description.text = experience.Localize("InputCalibrationDescription");
                    value.text = experience.CalibrationMeasurementCount.ToString(CultureInfo.InvariantCulture)
                        + " / " + RadialInputCalibration.MeasurementBeatCount.ToString(CultureInfo.InvariantCulture)
                        + "\nGuard: " + experience.GuardBinding;
                    SetVisible(false, false, false, false);
                    break;
                default:
                    heading.text = experience.Localize("InputTimingCalibration");
                    description.text = experience.HasCalibrationResult
                        ? FormatResult(experience)
                        : experience.Localize("NotEnoughSamples");
                    value.text = string.Empty;
                    SetVisible(false, true, true, true);
                    primaryButton.interactable = experience.HasCalibrationResult;
                    PulseForgeM9HViewBuilder.SetButtonLabel(primaryButton, experience.Localize("ApplySuggested"));
                    PulseForgeM9HViewBuilder.SetButtonLabel(retryButton, experience.Localize("Retry"));
                    PulseForgeM9HViewBuilder.SetButtonLabel(keepButton, experience.Localize("KeepCurrent"));
                    break;
            }
        }

        public override void CollectValidationErrors(List<string> errors)
        {
            base.CollectValidationErrors(errors);
            PulseForgeUIValidation.AddMissing(errors, primaryButton, "M9H Calibration: primary button is missing.");
            PulseForgeUIValidation.AddMissing(errors, cancelButton, "M9H Calibration: cancel button is missing.");
        }

        private void Primary()
        {
            PulseForgeExperienceCoordinator experience = controller?.Experience;
            if (experience == null) return;
            switch (experience.CalibrationStep)
            {
                case CalibrationStep.AudioVisualAlignment:
                    experience.ApplyAudioVisualAlignment();
                    break;
                case CalibrationStep.InputInstructions:
                    experience.StartInputMeasurement();
                    break;
                case CalibrationStep.InputResult:
                    experience.ApplySuggestedInputOffset();
                    break;
            }
        }

        private void Retry()
        {
            PulseForgeExperienceCoordinator experience = controller?.Experience;
            if (experience == null) return;
            if (experience.CalibrationStep == CalibrationStep.AudioVisualAlignment)
            {
                experience.RetryAudioVisualAlignment();
            }
            else
            {
                experience.RetryInputCalibration();
            }
        }

        private void KeepCurrent()
        {
            PulseForgeExperienceCoordinator experience = controller?.Experience;
            if (experience == null) return;
            if (experience.CalibrationStep == CalibrationStep.AudioVisualAlignment)
            {
                experience.KeepCurrentAudioVisualAlignment();
            }
            else if (experience.CalibrationStep == CalibrationStep.InputResult)
            {
                experience.KeepCurrentInputOffset();
            }
        }

        private void SetVisible(bool adjustment, bool primary, bool retry, bool keep)
        {
            decreaseButton.gameObject.SetActive(adjustment);
            increaseButton.gameObject.SetActive(adjustment);
            primaryButton.gameObject.SetActive(primary);
            retryButton.gameObject.SetActive(retry);
            keepButton.gameObject.SetActive(keep);
        }

        private static string FormatResult(PulseForgeExperienceCoordinator experience)
        {
            RadialInputCalibrationResult result = experience.CalibrationResult;
            string confidence = experience.Localize(result.Confidence.ToString());
            return experience.Localize("SuggestedOffset") + ": "
                + (result.SuggestedInputOffsetSeconds * 1000d).ToString(
                    "+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " ms\n"
                + experience.Localize("ValidSamples") + ": " + result.ValidSampleCount + "\n"
                + experience.Localize("MedianDeviation") + ": "
                + result.MedianDeviationMilliseconds.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " ms\n"
                + experience.Localize("Jitter") + ": "
                + result.JitterMilliseconds.ToString("0.0", CultureInfo.InvariantCulture) + " ms\n"
                + experience.Localize("Confidence") + ": " + confidence;
        }
    }
}
