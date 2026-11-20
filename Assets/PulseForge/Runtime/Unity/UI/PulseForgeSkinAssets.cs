using UnityEngine;

namespace PulseForge.Runtime.Unity.UI
{
    /// <summary>
    /// Procedural, asset-free skin primitives for the "forge at night" look:
    /// chamfered plates, plate frames, soft glows, thin rings and heat gradients.
    /// Everything is generated once per session and cached.
    /// </summary>
    internal static class PulseForgeSkinAssets
    {
        private const int PlateSize = 64;
        private const float PlateChamfer = 14f;
        private const float PlateBorder = 20f;
        private const float FrameStroke = 2f;

        private static Sprite plate;
        private static Sprite frame;
        private static Sprite glow;
        private static Sprite ring;
        private static Sprite gradient;
        private static Font gameFont;
        private static bool fontResolved;

        /// <summary>Filled plate with the top-left and bottom-right corners cut. 9-sliced.</summary>
        public static Sprite Plate => plate != null ? plate : plate = CreatePlate(false);

        /// <summary>Stroke-only version of <see cref="Plate"/>, for borders. 9-sliced.</summary>
        public static Sprite Frame => frame != null ? frame : frame = CreatePlate(true);

        /// <summary>Soft radial falloff, white with alpha.</summary>
        public static Sprite Glow => glow != null ? glow : glow = CreateGlow();

        /// <summary>Thin anti-aliased ring, white with alpha.</summary>
        public static Sprite Ring => ring != null ? ring : ring = CreateRing();

        /// <summary>Vertical alpha ramp, opaque at the bottom.</summary>
        public static Sprite Gradient => gradient != null ? gradient : gradient = CreateGradient();

        /// <summary>
        /// Condensed display face from the OS (Bahnschrift ships with Windows 10+), falling back to the
        /// built-in legacy font. Returns null only when neither is available.
        /// </summary>
        public static Font GameFont
        {
            get
            {
                if (fontResolved)
                {
                    return gameFont;
                }

                fontResolved = true;
                try
                {
                    string[] installed = Font.GetOSInstalledFontNames();
                    string[] preferred = { "Bahnschrift", "Segoe UI Semibold", "Segoe UI" };
                    for (int p = 0; p < preferred.Length && gameFont == null; p++)
                    {
                        for (int i = 0; i < installed.Length; i++)
                        {
                            if (installed[i] == preferred[p])
                            {
                                gameFont = Font.CreateDynamicFontFromOSFont(preferred[p], 32);
                                break;
                            }
                        }
                    }
                }
                catch (System.Exception)
                {
                    gameFont = null;
                }

                if (gameFont == null)
                {
                    gameFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                return gameFont;
            }
        }

        private static Sprite CreatePlate(bool strokeOnly)
        {
            Texture2D texture = NewTexture(PlateSize, PlateSize, strokeOnly ? "PF Skin Frame" : "PF Skin Plate");
            Color32[] pixels = new Color32[PlateSize * PlateSize];
            float size = PlateSize;
            float inv = 1f / Mathf.Sqrt(2f);
            for (int y = 0; y < PlateSize; y++)
            {
                for (int x = 0; x < PlateSize; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float d = Mathf.Max(
                        Mathf.Max(-px, px - size),
                        Mathf.Max(-py, py - size));
                    // top-left cut: x + (size - y) >= chamfer
                    d = Mathf.Max(d, (PlateChamfer - (px + (size - py))) * inv);
                    // bottom-right cut: (size - x) + y >= chamfer
                    d = Mathf.Max(d, (PlateChamfer - ((size - px) + py)) * inv);
                    float coverage = Mathf.Clamp01(0.5f - d);
                    if (strokeOnly)
                    {
                        coverage *= Mathf.Clamp01(d + FrameStroke + 0.5f);
                    }

                    pixels[y * PlateSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, PlateSize, PlateSize),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(PlateBorder, PlateBorder, PlateBorder, PlateBorder));
        }

        private static Sprite CreateGlow()
        {
            const int size = 128;
            Texture2D texture = NewTexture(size, size, "PF Skin Glow");
            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = (1f - r) * (1f - r);
                    a = a * a * (3f - 2f * a);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateRing()
        {
            const int size = 1024;
            const float stroke = 3f;
            Texture2D texture = NewTexture(size, size, "PF Skin Ring");
            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f;
            float radius = half - stroke - 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half;
                    float dy = y + 0.5f - half;
                    float d = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - radius) - stroke * 0.5f;
                    float a = Mathf.Clamp01(0.5f - d);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateGradient()
        {
            const int width = 4;
            const int height = 128;
            Texture2D texture = NewTexture(width, height, "PF Skin Gradient");
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float t = 1f - (y + 0.5f) / height;
                float a = t * t;
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Texture2D NewTexture(int width, int height, string name)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            return texture;
        }
    }
}
