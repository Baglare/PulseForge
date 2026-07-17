using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    internal static class PulseForgeM9HViewBuilder
    {
        public static RectTransform CreateCard(
            string name,
            Transform parent,
            Vector2 size,
            out GameObject root)
        {
            RectTransform card = FlowPanelBuilder.CreateScreenWithCard(name, parent, size, out root);
            root.GetComponent<Image>().color = PulseForgeUITheme.Backdrop;
            return card;
        }

        public static RectTransform AddButtonRow(RectTransform card, float height = 58f)
        {
            RectTransform row = PulseForgeUIFactory.CreateRect("Actions", card);
            PulseForgeUIFactory.SetLayoutHeight(row, height);
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return row;
        }

        public static void SetButtonLabel(Button button, string value)
        {
            Text label = button == null ? null : button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = value ?? string.Empty;
            }
        }

        public static bool IsPointerReleasedOver(Button button)
        {
            if (button == null
                || !button.isActiveAndEnabled
                || !button.IsInteractable()
                || Mouse.current == null
                || !Mouse.current.leftButton.wasReleasedThisFrame)
            {
                return false;
            }

            Canvas canvas = button.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.RectangleContainsScreenPoint(
                button.transform as RectTransform,
                Mouse.current.position.ReadValue(),
                eventCamera);
        }
    }
}
