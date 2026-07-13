using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    internal static class PulseForgeUITheme
    {
        public static readonly Color Backdrop = new Color(0.025f, 0.035f, 0.055f, 0.98f);
        public static readonly Color Surface = new Color(0.055f, 0.075f, 0.11f, 0.97f);
        public static readonly Color SurfaceRaised = new Color(0.085f, 0.115f, 0.16f, 0.98f);
        public static readonly Color SurfaceSoft = new Color(0.11f, 0.145f, 0.19f, 0.88f);
        public static readonly Color Overlay = new Color(0.015f, 0.025f, 0.045f, 0.72f);
        public static readonly Color PrimaryText = new Color(0.95f, 0.965f, 0.94f, 1f);
        public static readonly Color SecondaryText = new Color(0.58f, 0.68f, 0.79f, 1f);
        public static readonly Color Divider = new Color(0.22f, 0.31f, 0.42f, 0.8f);
        public static readonly Color Primary = new Color(0.25f, 0.53f, 0.95f, 1f);
        public static readonly Color Guard = new Color(0.12f, 0.82f, 0.96f, 1f);
        public static readonly Color Strike = new Color(1f, 0.36f, 0.16f, 1f);
        public static readonly Color Perfect = new Color(1f, 0.83f, 0.25f, 1f);
        public static readonly Color Good = new Color(0.20f, 0.88f, 0.62f, 1f);
        public static readonly Color Miss = new Color(0.98f, 0.22f, 0.25f, 1f);
        public static readonly Color Disabled = new Color(0.20f, 0.24f, 0.29f, 0.72f);

        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;
        public const float ScreenMargin = 36f;
        public const float CardCornerInset = 44f;
        public const float StandardSpacing = 16f;
        public const float ButtonHeight = 62f;
        public const float LargeButtonHeight = 78f;
        public const int BodyFontSize = 24;
        public const int SmallFontSize = 20;
        public const int HeadingFontSize = 42;
        public const int TitleFontSize = 56;

        public static ColorBlock CreateButtonColors(Color accent, bool selected = false)
        {
            Color normal = selected
                ? Color.Lerp(accent, Color.white, 0.08f)
                : Color.Lerp(SurfaceSoft, accent, 0.22f);
            Color highlighted = Color.Lerp(normal, Color.white, 0.18f);
            Color pressed = Color.Lerp(normal, Color.black, 0.24f);
            Color selectedColor = Color.Lerp(accent, Color.white, 0.14f);

            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = normal;
            colors.highlightedColor = highlighted;
            colors.pressedColor = pressed;
            colors.selectedColor = selectedColor;
            colors.disabledColor = Disabled;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            return colors;
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }
}
