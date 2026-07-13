using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    internal static class FlowPanelBuilder
    {
        public static RectTransform CreateScreenWithCard(string name, Transform parent, Vector2 cardSize, out GameObject root)
        {
            RectTransform rootRect = PulseForgeUIFactory.CreatePanel(name, parent, PulseForgeUITheme.Backdrop);
            root = rootRect.gameObject;
            RectTransform card = PulseForgeUIFactory.CreateCenteredCard(
                name + " Card",
                rootRect,
                cardSize,
                PulseForgeUITheme.Surface);
            VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(52, 52, 44, 44);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return card;
        }

        public static Text AddHeading(RectTransform card, string value)
        {
            Text text = PulseForgeUIFactory.CreateText(
                "Heading", card, value, PulseForgeUITheme.HeadingFontSize,
                PulseForgeUITheme.PrimaryText, TextAnchor.MiddleCenter, FontStyle.Bold);
            PulseForgeUIFactory.SetLayoutHeight(text, 76f);
            return text;
        }

        public static Text AddValue(RectTransform card, string value)
        {
            Text text = PulseForgeUIFactory.CreateText(
                value, card, value, PulseForgeUITheme.BodyFontSize,
                PulseForgeUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            PulseForgeUIFactory.SetLayoutHeight(text, 52f);
            return text;
        }

        public static Text AddCenteredText(RectTransform card, string value, int fontSize, Color color, float height)
        {
            Text text = PulseForgeUIFactory.CreateText(value, card, value, fontSize, color, TextAnchor.MiddleCenter);
            PulseForgeUIFactory.SetLayoutHeight(text, height);
            return text;
        }

        public static Button AddButton(
            RectTransform card,
            string label,
            Color accent,
            float height = PulseForgeUITheme.ButtonHeight,
            int fontSize = PulseForgeUITheme.BodyFontSize)
        {
            Button button = PulseForgeUIFactory.CreateButton(label, card, label, accent, fontSize);
            PulseForgeUIFactory.SetLayoutHeight(button, height);
            return button;
        }
    }
}
