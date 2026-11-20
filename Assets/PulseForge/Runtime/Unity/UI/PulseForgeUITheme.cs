using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    internal enum PulseForgeButtonStyle
    {
        Primary,
        Secondary,
        Subtle,
        Danger,
        Segment,
        SelectedSegment
    }

    internal static class PulseForgeUITheme
    {
        // Forge-at-night palette: warm blackened iron, ember accent, tempered-steel guard.
        public static readonly Color Backdrop = Hex(0x0B, 0x09, 0x08);
        public static readonly Color BackgroundSecondary = Hex(0x13, 0x0F, 0x0D);
        public static readonly Color Surface = Hex(0x17, 0x12, 0x0F, 0.96f);
        public static readonly Color SurfaceRaised = Hex(0x22, 0x1A, 0x15, 0.98f);
        public static readonly Color SurfaceSoft = Hex(0x2C, 0x22, 0x1B, 0.90f);
        public static readonly Color Overlay = Hex(0x06, 0x04, 0x03, 0.82f);
        public static readonly Color PrimaryText = Hex(0xF5, 0xEC, 0xE2);
        public static readonly Color SecondaryText = Hex(0xA9, 0x98, 0x8A);
        public static readonly Color Divider = Hex(0x3B, 0x2E, 0x25, 0.82f);
        public static readonly Color Border = Hex(0x45, 0x36, 0x2B);
        public static readonly Color Primary = Hex(0xFF, 0x7A, 0x1F);
        public static readonly Color AccentSoft = Hex(0x7A, 0x34, 0x10);
        public static readonly Color Guard = Hex(0x35, 0xD3, 0xF3);
        public static readonly Color Strike = Hex(0xFF, 0x75, 0x47);
        public static readonly Color Perfect = Hex(0xFF, 0xD1, 0x66);
        public static readonly Color Good = Hex(0x50, 0xD8, 0x90);
        public static readonly Color Miss = Hex(0xFF, 0x4F, 0x61);
        public static readonly Color Disabled = Hex(0x26, 0x1F, 0x1A, 0.72f);
        public static readonly Color DarkText = Hex(0x1A, 0x0B, 0x03);
        public static readonly Color Ember = Hex(0xFF, 0x7A, 0x1F);
        public static readonly Color EmberDeep = Hex(0xC2, 0x3B, 0x0C);
        public static readonly Color Heat = Hex(0xFF, 0xB8, 0x4D);

        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;
        public const float ScreenMargin = PulseForgeUILayout.ScreenMargin;
        public const float StandardSpacing = PulseForgeUILayout.StandardSpacing;
        public const float ButtonHeight = PulseForgeUILayout.ButtonHeight;
        public const float LargeButtonHeight = PulseForgeUILayout.LargeButtonHeight;
        public const int BodyFontSize = PulseForgeUITypography.Body;
        public const int SmallFontSize = PulseForgeUITypography.Secondary;
        public const int HeadingFontSize = PulseForgeUITypography.ScreenHeading;
        public const int TitleFontSize = PulseForgeUITypography.AppTitle;

        public static ColorBlock CreateButtonColors(Color accent, bool selected = false)
        {
            PulseForgeButtonStyle style = selected
                ? PulseForgeButtonStyle.SelectedSegment
                : ResolveButtonStyle(accent);
            return CreateButtonColors(style);
        }

        public static ColorBlock CreateButtonColors(PulseForgeButtonStyle style)
        {
            Color normal;
            Color highlighted;
            Color pressed;
            Color selected;
            switch (style)
            {
                case PulseForgeButtonStyle.Primary:
                    normal = Primary;
                    highlighted = Color.Lerp(Primary, Color.white, 0.16f);
                    pressed = Color.Lerp(Primary, Color.black, 0.22f);
                    selected = highlighted;
                    break;
                case PulseForgeButtonStyle.Danger:
                    normal = Miss;
                    highlighted = Color.Lerp(Miss, Color.white, 0.14f);
                    pressed = Color.Lerp(Miss, Color.black, 0.24f);
                    selected = highlighted;
                    break;
                case PulseForgeButtonStyle.SelectedSegment:
                    normal = Color.Lerp(SurfaceRaised, Primary, 0.30f);
                    normal.a = 1f;
                    highlighted = Color.Lerp(normal, Primary, 0.18f);
                    pressed = Color.Lerp(normal, Color.black, 0.20f);
                    selected = normal;
                    break;
                case PulseForgeButtonStyle.Secondary:
                    normal = SurfaceRaised;
                    highlighted = Color.Lerp(SurfaceRaised, AccentSoft, 0.24f);
                    pressed = Color.Lerp(SurfaceRaised, Color.black, 0.22f);
                    selected = Color.Lerp(SurfaceRaised, AccentSoft, 0.34f);
                    break;
                case PulseForgeButtonStyle.Subtle:
                    normal = WithAlpha(SurfaceSoft, 0.46f);
                    highlighted = WithAlpha(SurfaceSoft, 0.82f);
                    pressed = Color.Lerp(SurfaceSoft, Color.black, 0.28f);
                    selected = SurfaceSoft;
                    break;
                default:
                    normal = WithAlpha(SurfaceRaised, 0.86f);
                    highlighted = Color.Lerp(SurfaceRaised, AccentSoft, 0.30f);
                    pressed = Color.Lerp(SurfaceRaised, Color.black, 0.25f);
                    selected = Color.Lerp(SurfaceRaised, AccentSoft, 0.26f);
                    break;
            }

            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = normal;
            colors.highlightedColor = highlighted;
            colors.pressedColor = pressed;
            colors.selectedColor = selected;
            colors.disabledColor = style == PulseForgeButtonStyle.Primary
                ? Color.Lerp(Primary, SecondaryText, 0.55f)
                : Disabled;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            return colors;
        }

        public static PulseForgeButtonStyle ResolveButtonStyle(Color accent)
        {
            if (Approximately(accent, Miss))
            {
                return PulseForgeButtonStyle.Danger;
            }

            if (Approximately(accent, Primary))
            {
                return PulseForgeButtonStyle.Primary;
            }

            if (Approximately(accent, SurfaceSoft))
            {
                return PulseForgeButtonStyle.Subtle;
            }

            return PulseForgeButtonStyle.Secondary;
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static Color Hex(int red, int green, int blue, float alpha = 1f)
        {
            return new Color(red / 255f, green / 255f, blue / 255f, alpha);
        }

        private static bool Approximately(Color left, Color right)
        {
            return Mathf.Abs(left.r - right.r) < 0.01f
                && Mathf.Abs(left.g - right.g) < 0.01f
                && Mathf.Abs(left.b - right.b) < 0.01f;
        }
    }

    internal static class PulseForgeUILayout
    {
        public const float ScreenMargin = 32f;
        public const float StandardSpacing = 12f;
        public const float TightSpacing = 8f;
        public const float PanelPadding = 42f;
        public const float ButtonHeight = 54f;
        public const float LargeButtonHeight = 68f;
        public const float CardBorderThickness = 1f;
        public const float OverlayAlpha = 0.76f;
        public const float TopHudHeight = 94f;
        public const float LaneHudHeight = 214f;
        public const float LaneHeight = 72f;
        public const float HitZoneWidth = 5f;
    }

    internal static class PulseForgeUITypography
    {
        public const int AppTitle = 48;
        public const int ScreenHeading = 32;
        public const int SectionHeading = 17;
        public const int MainButton = 18;
        public const int Body = 16;
        public const int Secondary = 13;
        public const int HudLabel = 13;
        public const int HudValue = 24;
    }
}
