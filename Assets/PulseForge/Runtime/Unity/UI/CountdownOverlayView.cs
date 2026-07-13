using System.Collections.Generic;
using PulseForge.Runtime.Unity.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class CountdownOverlayView : PulseForgePanelView
    {
        [SerializeField] private Text countdownText;

        public static CountdownOverlayView Create(Transform parent)
        {
            RectTransform root = PulseForgeUIFactory.CreateStretchRect("Countdown Overlay", parent);
            CountdownOverlayView view = root.gameObject.AddComponent<CountdownOverlayView>();
            view.ConfigurePanelRoot(root.gameObject);
            view.countdownText = PulseForgeUIFactory.CreateText(
                "Countdown",
                root,
                string.Empty,
                132,
                PulseForgeUITheme.PrimaryText,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            view.countdownText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            view.countdownText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            view.countdownText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            view.countdownText.rectTransform.sizeDelta = new Vector2(520f, 220f);
            view.countdownText.rectTransform.anchoredPosition = new Vector2(0f, 40f);
            return view;
        }

        public void Refresh(DebugRhythmPrototypeController controller)
        {
            countdownText.text = Mathf.CeilToInt((float)controller.CountdownRemainingSeconds).ToString();
        }

        public override void CollectValidationErrors(List<string> errors)
        {
            base.CollectValidationErrors(errors);
            PulseForgeUIValidation.AddMissing(errors, countdownText, "Countdown: text is missing.");
        }
    }
}
