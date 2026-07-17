using System;
using PulseForge.Runtime.Unity.Onboarding;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public static class PulseForgeM9HUISetup
    {
        public static void Apply(
            PulseForgeSceneUIRoot root,
            Action<GameObject> registerCreated = null)
        {
            if (root == null || root.Canvas == null)
            {
                return;
            }

            FirstTimeSetupView firstTime = root.FirstTimeSetupView
                ?? root.GetComponentInChildren<FirstTimeSetupView>(true);
            CalibrationView calibration = root.CalibrationView
                ?? root.GetComponentInChildren<CalibrationView>(true);
            TrainingLessonSelectView select = root.TrainingLessonSelectView
                ?? root.GetComponentInChildren<TrainingLessonSelectView>(true);
            ActiveTrainingView active = root.ActiveTrainingView
                ?? root.GetComponentInChildren<ActiveTrainingView>(true);
            TrainingResultView result = root.TrainingResultView
                ?? root.GetComponentInChildren<TrainingResultView>(true);

            if (firstTime == null)
            {
                firstTime = FirstTimeSetupView.Create(root.Canvas.transform);
                registerCreated?.Invoke(firstTime.gameObject);
            }
            if (calibration == null)
            {
                calibration = CalibrationView.Create(root.Canvas.transform);
                registerCreated?.Invoke(calibration.gameObject);
            }
            if (select == null)
            {
                select = TrainingLessonSelectView.Create(root.Canvas.transform);
                registerCreated?.Invoke(select.gameObject);
            }
            if (active == null)
            {
                active = ActiveTrainingView.Create(root.Canvas.transform);
                registerCreated?.Invoke(active.gameObject);
            }
            if (result == null)
            {
                result = TrainingResultView.Create(root.Canvas.transform);
                registerCreated?.Invoke(result.gameObject);
            }

            active.EnsureTimingBar(registerCreated);
            PrepareTrainingInteraction(select);
            PrepareTrainingInteraction(active);
            PrepareTrainingInteraction(result);

            root.ConfigureM9H(firstTime, calibration, select, active, result);
            root.SettingsPanel?.EnsureM9HControls(registerCreated);
            root.ApplyM9HVisibility(PulseForgeExperienceView.None);
        }

        private static void PrepareTrainingInteraction(PulseForgePanelView view)
        {
            if (view == null || view.PanelRoot == null)
            {
                return;
            }

            Image screenImage = view.PanelRoot.GetComponent<Image>();
            if (screenImage != null)
            {
                screenImage.raycastTarget = false;
            }

            Button[] buttons = view.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                button.interactable = true;
                if (button.targetGraphic != null)
                {
                    button.targetGraphic.raycastTarget = true;
                }
            }
        }
    }
}
