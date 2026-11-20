using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    /// <summary>
    /// Runtime "forge at night" skin. Runs after every UI setup module so it restyles both the
    /// scene-authored hierarchy and anything the setup modules created. It only changes presentation
    /// (colours, sprites, fonts, positions); bindings, names and hierarchy parents stay intact.
    /// </summary>
    public static class PulseForgeGameSkin
    {
        internal const string LayersName = "Forge Skin Layers";
        private const string AccentName = "Skin Accent";
        private const string LoadoutPanelName = "Skin Loadout Panel";
        private const string LoadoutHeaderName = "Skin Loadout Header";
        private const string TuningHeaderName = "Skin Tuning Header";
        private const string TitleAccentName = "Skin Title Accent";

        // The previous navy palette, used to recolour scene-serialized graphics into the forge palette.
        private static readonly KeyValuePair<Color, Color>[] LegacyPalette =
        {
            Pair(0x08, 0x0D, 0x16, PulseForgeUITheme.Backdrop),
            Pair(0x0D, 0x14, 0x20, PulseForgeUITheme.BackgroundSecondary),
            Pair(0x12, 0x1C, 0x2A, PulseForgeUITheme.Surface),
            Pair(0x18, 0x25, 0x36, PulseForgeUITheme.SurfaceRaised),
            Pair(0x1B, 0x2A, 0x3C, PulseForgeUITheme.SurfaceSoft),
            Pair(0x05, 0x09, 0x11, PulseForgeUITheme.Overlay),
            Pair(0xEA, 0xF2, 0xF8, PulseForgeUITheme.PrimaryText),
            Pair(0x8F, 0xA3, 0xB8, PulseForgeUITheme.SecondaryText),
            Pair(0x29, 0x3B, 0x50, PulseForgeUITheme.Border),
            Pair(0x3B, 0xBF, 0xF3, PulseForgeUITheme.Primary),
            Pair(0x1D, 0x6F, 0x95, PulseForgeUITheme.AccentSoft),
            Pair(0x23, 0x30, 0x40, PulseForgeUITheme.Disabled),
            Pair(0x05, 0x12, 0x1B, PulseForgeUITheme.DarkText)
        };

        public static void Apply(PulseForgeSceneUIRoot root)
        {
            if (root == null || root.Canvas == null)
            {
                return;
            }

            Transform canvas = root.Canvas.transform;
            RecolorLegacyGraphics(canvas);
            ApplyFonts(canvas);
            StyleBackground(root);
            StyleScreens(root);
            StyleUnskinnedButtons(canvas);
            StyleToggles(canvas);
            ComposeSetup(root);
            ComposeReady(root);
            ComposeResults(root);
            StyleHud(root);

            if (root.MotionController != null)
            {
                // Poses are cached on Configure; refresh them so entry motion lands on the new layout.
                root.MotionController.Configure(root, root.MotionController.Runner);
            }
        }

        // ---------------------------------------------------------------- global passes

        private static void RecolorLegacyGraphics(Transform canvas)
        {
            Graphic[] graphics = canvas.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Color current = graphics[i].color;
                for (int p = 0; p < LegacyPalette.Length; p++)
                {
                    if (SameRgb(current, LegacyPalette[p].Key))
                    {
                        Color replacement = LegacyPalette[p].Value;
                        replacement.a = current.a;
                        graphics[i].color = replacement;
                        break;
                    }
                }
            }
        }

        private static void ApplyFonts(Transform canvas)
        {
            Font font = PulseForgeUIFactory.DefaultFont;
            if (font == null)
            {
                return;
            }

            Text[] texts = canvas.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].font = font;
            }
        }

        private static void StyleUnskinnedButtons(Transform canvas)
        {
            Button[] buttons = canvas.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Image image = buttons[i].targetGraphic as Image;
                if (image != null && image.sprite == PulseForgeSkinAssets.Plate && image.color == Color.white)
                {
                    continue;
                }

                PulseForgeUIVisualStyle.ApplyButtonStyle(buttons[i], InferStyle(buttons[i]));
            }
        }

        private static void StyleToggles(Transform canvas)
        {
            Toggle[] toggles = canvas.GetComponentsInChildren<Toggle>(true);
            for (int i = 0; i < toggles.Length; i++)
            {
                Image box = toggles[i].targetGraphic as Image;
                if (box != null)
                {
                    box.sprite = PulseForgeSkinAssets.Plate;
                    box.type = Image.Type.Sliced;
                    box.pixelsPerUnitMultiplier = 3f;
                    box.color = PulseForgeUITheme.SurfaceRaised;
                    PulseForgeUIVisualStyle.ApplyFrame(box.transform, PulseForgeUITheme.Border, 3f);
                }

                Image check = toggles[i].graphic as Image;
                if (check != null)
                {
                    check.sprite = PulseForgeSkinAssets.Plate;
                    check.type = Image.Type.Sliced;
                    check.pixelsPerUnitMultiplier = 4f;
                    check.color = PulseForgeUITheme.Ember;
                }

                toggles[i].colors = PulseForgeUITheme.CreateButtonColors(PulseForgeButtonStyle.Subtle);
                ColorBlock colors = toggles[i].colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.15f, 1.1f, 1.05f, 1f);
                colors.selectedColor = Color.white;
                toggles[i].colors = colors;
            }
        }

        private static PulseForgeButtonStyle InferStyle(Button button)
        {
            Color c = button.colors.normalColor;
            string name = button.gameObject.name;
            if (button.transform.parent != null && button.transform.parent.name == "Options")
            {
                return PulseForgeButtonStyle.Segment;
            }

            if (name == "Start" || name == "Analyze Song" || name == "Resume" || name == "Replay" || name == "Retry")
            {
                return PulseForgeButtonStyle.Primary;
            }

            if (SameRgb(c, PulseForgeUITheme.Primary) || (c.b > 0.6f && c.r < 0.4f && c.g > 0.5f))
            {
                return PulseForgeButtonStyle.Primary;
            }

            if (SameRgb(c, PulseForgeUITheme.Miss) || (c.r > 0.8f && c.g < 0.45f))
            {
                return PulseForgeButtonStyle.Danger;
            }

            return c.a < 0.6f ? PulseForgeButtonStyle.Subtle : PulseForgeButtonStyle.Secondary;
        }

        // ---------------------------------------------------------------- background

        private static void StyleBackground(PulseForgeSceneUIRoot root)
        {
            GameObject background = root.Background;
            if (background == null)
            {
                return;
            }

            Image baseImage = background.GetComponent<Image>();
            if (baseImage != null)
            {
                baseImage.color = PulseForgeUITheme.Backdrop;
                baseImage.raycastTarget = false;
            }

            Transform legacy = background.transform.Find(PulseForgeUIVisualStyle.BackgroundLayersName);
            if (legacy != null)
            {
                legacy.gameObject.SetActive(false);
            }

            Transform existing = background.transform.Find(LayersName);
            if (existing != null)
            {
                return;
            }

            RectTransform layers = PulseForgeUIFactory.CreateStretchRect(LayersName, background.transform);
            layers.SetAsLastSibling();

            // Heat rising from the floor of the forge.
            Image heat = AddImage("Floor Heat", layers, PulseForgeSkinAssets.Gradient, PulseForgeUITheme.WithAlpha(PulseForgeUITheme.EmberDeep, 0.30f));
            RectTransform heatRect = heat.rectTransform;
            heatRect.anchorMin = new Vector2(0f, 0f);
            heatRect.anchorMax = new Vector2(1f, 0.55f);
            heatRect.offsetMin = Vector2.zero;
            heatRect.offsetMax = Vector2.zero;

            Image floorGlow = AddImage("Floor Glow", layers, PulseForgeSkinAssets.Glow, PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Ember, 0.26f));
            Place(floorGlow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -120f), new Vector2(3000f, 760f));

            // Ceiling shade keeps the top calm for titles and HUD.
            Image shade = AddImage("Ceiling Shade", layers, PulseForgeSkinAssets.Gradient, PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Backdrop, 0.85f));
            RectTransform shadeRect = shade.rectTransform;
            shadeRect.anchorMin = new Vector2(0f, 0.72f);
            shadeRect.anchorMax = new Vector2(1f, 1f);
            shadeRect.offsetMin = Vector2.zero;
            shadeRect.offsetMax = Vector2.zero;
            shadeRect.localScale = new Vector3(1f, -1f, 1f);

            // The pulse: concentric rings around a molten core, breathing on the beat.
            RectTransform pulse = PulseForgeUIFactory.CreateRect("Pulse", layers);
            pulse.anchorMin = new Vector2(0.27f, 0.52f);
            pulse.anchorMax = new Vector2(0.27f, 0.52f);
            pulse.pivot = new Vector2(0.5f, 0.5f);
            pulse.sizeDelta = Vector2.zero;
            pulse.anchoredPosition = Vector2.zero;

            Image core = AddImage("Core", pulse, PulseForgeSkinAssets.Glow, PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Ember, 0.30f));
            Place(core.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 980f));

            float[] sizes = { 440f, 660f, 900f, 1180f, 1500f, 1880f };
            float[] alphas = { 0.30f, 0.22f, 0.16f, 0.11f, 0.075f, 0.05f };
            RectTransform[] rings = new RectTransform[sizes.Length];
            for (int i = 0; i < sizes.Length; i++)
            {
                Color tone = Color.Lerp(PulseForgeUITheme.Heat, PulseForgeUITheme.EmberDeep, i / (float)(sizes.Length - 1));
                Image ring = AddImage("Ring " + (i + 1), pulse, PulseForgeSkinAssets.Ring, PulseForgeUITheme.WithAlpha(tone, alphas[i]));
                Place(ring.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(sizes[i], sizes[i]));
                rings[i] = ring.rectTransform;
            }

            PulseForgeGameSkinPulse animator = pulse.gameObject.AddComponent<PulseForgeGameSkinPulse>();
            animator.Configure(root, core, rings, alphas);
        }

        // ---------------------------------------------------------------- screens

        private static void StyleScreens(PulseForgeSceneUIRoot root)
        {
            ClearScreenRoot(root.SetupPanel);
            ClearScreenRoot(root.ProcessingPanel);
            ClearScreenRoot(root.ReadyPanel);
            ClearScreenRoot(root.ResultsPanel);
            ClearScreenRoot(root.ErrorPanel);
            ClearScreenRoot(root.SavedTracksPanel, 0.82f);
            ClearScreenRoot(root.SettingsPanel, 0.82f);
            ClearScreenRoot(root.FirstTimeSetupView, 0.35f);
            ClearScreenRoot(root.CalibrationView, 0.35f);
            ClearScreenRoot(root.TrainingLessonSelectView, 0.35f);
            ClearScreenRoot(root.TrainingResultView, 0.35f);

            StyleCards(root.Canvas.transform);
            StyleHeadings(root.Canvas.transform);
        }

        private static void ClearScreenRoot(Component view, float shade = 0f)
        {
            PulseForgePanelView panel = view as PulseForgePanelView;
            GameObject panelRoot = panel != null ? panel.PanelRoot : view == null ? null : view.gameObject;
            Image image = panelRoot == null ? null : panelRoot.GetComponent<Image>();
            if (image != null)
            {
                image.color = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Backdrop, shade);
            }
        }

        private static void StyleCards(Transform canvas)
        {
            RectTransform[] rects = canvas.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; i++)
            {
                string name = rects[i].gameObject.name;
                if (!name.EndsWith(" Card") || name == "Setup Card" || name == "Selected Audio Card")
                {
                    continue;
                }

                Image image = rects[i].GetComponent<Image>();
                if (image == null || rects[i].GetComponent<Button>() != null)
                {
                    continue;
                }

                StylePlate(rects[i], PulseForgeUITheme.Surface, PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Border, 0.95f), 1f);
                AddAccent(rects[i], PulseForgeUITheme.Ember, new Vector2(30f, -10f), new Vector2(96f, 4f));
            }
        }

        private static void StyleHeadings(Transform canvas)
        {
            Text[] texts = canvas.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                string name = texts[i].gameObject.name;
                if (name == "Heading" || (name == "Title" && texts[i].fontSize >= PulseForgeUITypography.ScreenHeading))
                {
                    texts[i].fontStyle = FontStyle.Bold;
                    texts[i].fontSize = Mathf.Max(texts[i].fontSize, 40);
                    if (SameRgb(texts[i].color, PulseForgeUITheme.PrimaryText))
                    {
                        texts[i].color = PulseForgeUITheme.PrimaryText;
                    }
                }
            }
        }

        // ---------------------------------------------------------------- setup: title + loadout

        private static void ComposeSetup(PulseForgeSceneUIRoot root)
        {
            SetupPanelView setup = root.SetupPanel;
            RectTransform card = setup == null || setup.PanelRoot == null
                ? null
                : setup.PanelRoot.transform.Find("Setup Card") as RectTransform;
            if (card == null)
            {
                return;
            }

            // The card becomes the full-screen stage; its children are composed into two columns.
            card.anchorMin = Vector2.zero;
            card.anchorMax = Vector2.one;
            card.pivot = new Vector2(0.5f, 0.5f);
            card.offsetMin = Vector2.zero;
            card.offsetMax = Vector2.zero;
            card.anchoredPosition = Vector2.zero;
            Image cardImage = card.GetComponent<Image>();
            if (cardImage != null)
            {
                cardImage.color = Color.clear;
                cardImage.raycastTarget = false;
            }

            DisableOutline(card);
            LayoutGroup cardLayout = card.GetComponent<LayoutGroup>();
            if (cardLayout != null)
            {
                cardLayout.enabled = false;
            }

            ContentSizeFitter fitter = card.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.enabled = false;
            }

            // ---- left column: identity, track, the forge button
            const float leftX = 132f;
            const float leftWidth = 680f;

            RectTransform titleAccent = EnsureChild(card, TitleAccentName, out Image accentImage);
            accentImage.sprite = null;
            accentImage.color = PulseForgeUITheme.Ember;
            PlaceLeft(titleAccent, leftX, 214f, 72f, 6f);

            Text title = FindText(card, "Title");
            if (title != null)
            {
                title.text = "PULSEFORGE";
                title.fontSize = 118;
                title.fontStyle = FontStyle.Bold;
                title.color = PulseForgeUITheme.PrimaryText;
                title.alignment = TextAnchor.MiddleLeft;
                title.horizontalOverflow = HorizontalWrapMode.Overflow;
                title.resizeTextForBestFit = false;
                PlaceLeft(title.rectTransform, leftX - 6f, 228f, leftWidth + 120f, 132f);
                Shadow shadow = GetOrAdd<Shadow>(title.gameObject);
                shadow.effectColor = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.EmberDeep, 0.55f);
                shadow.effectDistance = new Vector2(0f, -6f);
            }

            Text description = FindText(card, "Description");
            if (description != null)
            {
                description.fontSize = 24;
                description.color = PulseForgeUITheme.SecondaryText;
                description.alignment = TextAnchor.UpperLeft;
                PlaceLeft(description.rectTransform, leftX, 378f, leftWidth, 64f);
            }

            Text sourceLabel = FindText(card, "Audio Source Label");
            if (sourceLabel != null)
            {
                StyleKicker(sourceLabel);
                PlaceLeft(sourceLabel.rectTransform, leftX, 452f, leftWidth, 26f);
            }

            RectTransform selected = FindRect(card, "Selected Audio Card");
            if (selected != null)
            {
                StylePlate(selected, PulseForgeUITheme.WithAlpha(PulseForgeUITheme.SurfaceRaised, 0.92f), PulseForgeUITheme.Border, 1.3f);
                AddAccent(selected, PulseForgeUITheme.Ember, new Vector2(0f, 0f), new Vector2(5f, 0f), true);
                PlaceLeft(selected, leftX, 484f, leftWidth, 66f);
                Text selectedText = FindText(selected, "Selected Audio");
                if (selectedText != null)
                {
                    selectedText.fontSize = 20;
                    selectedText.color = PulseForgeUITheme.PrimaryText;
                    selectedText.rectTransform.offsetMin = new Vector2(28f, 6f);
                    selectedText.rectTransform.offsetMax = new Vector2(-20f, -6f);
                }
            }

            RectTransform sourceActions = FindRect(card, "Audio Source Actions");
            if (sourceActions != null)
            {
                PlaceLeft(sourceActions, leftX, 564f, leftWidth, 56f);
                HorizontalLayoutGroup row = sourceActions.GetComponent<HorizontalLayoutGroup>();
                if (row != null)
                {
                    row.spacing = 12f;
                }

                PulseForgeUIVisualStyle.ApplyButtonStyle(FindButton(sourceActions, "Play Built-in Demo"), PulseForgeButtonStyle.Secondary);
                PulseForgeUIVisualStyle.ApplyButtonStyle(FindButton(sourceActions, "Choose Custom Audio"), PulseForgeButtonStyle.Secondary);
            }

            Button analyze = FindButton(card, "Analyze Song");
            if (analyze != null)
            {
                PulseForgeUIVisualStyle.ApplyButtonStyle(analyze, PulseForgeButtonStyle.Primary);
                PlaceLeft(analyze.GetComponent<RectTransform>(), leftX, 652f, leftWidth, 100f);
                Text label = analyze.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.fontSize = 36;
                    label.resizeTextMaxSize = 36;
                }
            }

            Text status = FindText(card, "Setup Status");
            if (status != null)
            {
                status.fontSize = 18;
                status.alignment = TextAnchor.MiddleLeft;
                PlaceLeft(status.rectTransform, leftX, 762f, leftWidth, 30f);
            }

            RectTransform library = FindRect(card, "Library Actions");
            if (library != null)
            {
                PlaceLeft(library, leftX, 862f, leftWidth, 50f);
                Toggle saveToggle = library.GetComponentInChildren<Toggle>(true);
                Text toggleLabel = saveToggle == null ? null : FindText(saveToggle.transform, "Label");
                if (toggleLabel != null)
                {
                    toggleLabel.fontSize = 17;
                    toggleLabel.color = PulseForgeUITheme.SecondaryText;
                    toggleLabel.resizeTextForBestFit = true;
                    toggleLabel.resizeTextMinSize = 12;
                    toggleLabel.resizeTextMaxSize = 17;
                }

                Button[] libraryButtons = library.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < libraryButtons.Length; i++)
                {
                    PulseForgeUIVisualStyle.ApplyButtonStyle(libraryButtons[i], PulseForgeButtonStyle.Subtle);
                }
            }

            // ---- right column: the loadout
            const float rightX = 132f;
            const float rightWidth = 780f;
            const float panelTop = 196f;
            const float rowHeight = 62f;
            const float rowStep = 76f;

            RectTransform panel = EnsureChild(card, LoadoutPanelName, out Image panelImage);
            panel.SetAsFirstSibling();
            StylePlate(panel, PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Surface, 0.88f), PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Border, 0.95f), 0.8f);
            panelImage.raycastTarget = false;
            AddAccent(panel, PulseForgeUITheme.Ember, new Vector2(40f, -14f), new Vector2(120f, 4f));

            RectTransform header = EnsureText(card, LoadoutHeaderName, "LOADOUT");
            StyleSectionHeader(header.GetComponent<Text>(), PulseForgeUITheme.PrimaryText, 30);
            PlaceRight(header, rightX + 44f, panelTop + 34f, rightWidth - 88f, 40f);

            string[] primary = { "Difficulty Selector", "Game Mode Selector", "Combat Style Selector" };
            string[] tuning = { "Coverage Selector", "Timing Assist Selector", "Detection Selector" };
            float y = panelTop + 100f;
            for (int i = 0; i < primary.Length; i++)
            {
                if (ComposeSelectorRow(card, primary[i], rightX + 44f, y, rightWidth - 88f, rowHeight, true))
                {
                    y += rowStep;
                }
            }

            RectTransform tuningHeader = EnsureText(card, TuningHeaderName, "TUNING");
            StyleSectionHeader(tuningHeader.GetComponent<Text>(), PulseForgeUITheme.SecondaryText, 18);
            PlaceRight(tuningHeader, rightX + 44f, y + 10f, rightWidth - 88f, 26f);
            y += 50f;
            for (int i = 0; i < tuning.Length; i++)
            {
                if (ComposeSelectorRow(card, tuning[i], rightX + 44f, y, rightWidth - 88f, 52f, false))
                {
                    y += 64f;
                }
            }

            PlaceRight(panel, rightX, panelTop, rightWidth, y - panelTop + 26f);
        }

        private static bool ComposeSelectorRow(RectTransform card, string selectorName, float x, float top, float width, float height, bool primary)
        {
            RectTransform selector = FindDirectChild(card, selectorName);
            if (selector == null)
            {
                return false;
            }

            PlaceRight(selector, x, top, width, height);
            LayoutGroup group = selector.GetComponent<LayoutGroup>();
            if (group != null)
            {
                group.enabled = false;
            }

            const float labelWidth = 210f;
            Text label = FindText(selector, "Label");
            if (label != null)
            {
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(0f, 1f);
                labelRect.pivot = new Vector2(0f, 0.5f);
                labelRect.offsetMin = new Vector2(0f, 0f);
                labelRect.offsetMax = new Vector2(labelWidth, 0f);
                label.fontSize = primary ? 22 : 18;
                label.fontStyle = FontStyle.Bold;
                label.color = primary ? PulseForgeUITheme.PrimaryText : PulseForgeUITheme.SecondaryText;
                label.alignment = TextAnchor.MiddleLeft;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 12;
                label.resizeTextMaxSize = label.fontSize;
            }

            RectTransform options = FindRect(selector, "Options");
            if (options != null)
            {
                options.anchorMin = new Vector2(0f, 0f);
                options.anchorMax = new Vector2(1f, 1f);
                options.pivot = new Vector2(0.5f, 0.5f);
                options.offsetMin = new Vector2(labelWidth, 0f);
                options.offsetMax = Vector2.zero;
                HorizontalLayoutGroup row = options.GetComponent<HorizontalLayoutGroup>();
                if (row != null)
                {
                    row.spacing = 8f;
                    row.childForceExpandWidth = true;
                    row.childControlWidth = true;
                    row.childControlHeight = true;
                    row.childForceExpandHeight = true;
                }

                LayoutElement optionsLayout = options.GetComponent<LayoutElement>();
                if (optionsLayout != null)
                {
                    optionsLayout.ignoreLayout = true;
                }

                Text[] optionLabels = options.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < optionLabels.Length; i++)
                {
                    optionLabels[i].fontSize = primary ? 19 : 16;
                    optionLabels[i].resizeTextMaxSize = optionLabels[i].fontSize;
                }
            }

            return true;
        }

        // ---------------------------------------------------------------- ready: briefing + loadout + start

        private static readonly HashSet<Transform> Placed = new HashSet<Transform>();

        private static void ComposeReady(PulseForgeSceneUIRoot root)
        {
            RectTransform card = FindCard(root.ReadyPanel, "Ready Panel Card");
            if (card == null)
            {
                return;
            }

            PrepareStageCard(card, new Vector2(1440f, 560f));
            const float leftX = 60f;
            const float leftWidth = 620f;
            const float rightX = 760f;
            const float rightWidth = 620f;

            Text heading = FindDirectText(card, "Heading");
            if (heading != null)
            {
                StyleKicker(heading);
                heading.fontSize = 20;
                PlaceInCard(heading.rectTransform, leftX, 44f, leftWidth, 30f);
            }

            Text song = FindDirectText(card, "Song");
            if (song != null)
            {
                song.fontSize = 40;
                song.fontStyle = FontStyle.Bold;
                song.color = PulseForgeUITheme.PrimaryText;
                song.alignment = TextAnchor.UpperLeft;
                song.resizeTextForBestFit = true;
                song.resizeTextMinSize = 20;
                song.resizeTextMaxSize = 40;
                PlaceInCard(song.rectTransform, leftX, 82f, leftWidth, 104f);
            }

            string[] stats = { "Events", "Input Cost", "Analysis Quality", "Detection", "Difficulty", "Combat Style" };
            for (int i = 0; i < stats.Length; i++)
            {
                Text stat = FindDirectText(card, stats[i]);
                if (stat == null)
                {
                    continue;
                }

                stat.fontSize = 19;
                stat.fontStyle = FontStyle.Bold;
                stat.color = PulseForgeUITheme.SecondaryText;
                stat.alignment = TextAnchor.MiddleLeft;
                stat.resizeTextForBestFit = true;
                stat.resizeTextMinSize = 12;
                stat.resizeTextMaxSize = 19;
                int column = i % 2;
                int row = i / 2;
                PlaceInCard(stat.rectTransform, leftX + column * 320f, 206f + row * 56f, 300f, 46f);
            }

            RectTransform loadout = EnsureText(card, LoadoutHeaderName, "LOADOUT");
            StyleSectionHeader(loadout.GetComponent<Text>(), PulseForgeUITheme.PrimaryText, 26);
            PlaceInCard(loadout, rightX, 40f, rightWidth, 36f);

            string[] selectors = { "Game Mode Selector", "Coverage Selector", "Timing Assist Selector" };
            float y = 98f;
            for (int i = 0; i < selectors.Length; i++)
            {
                RectTransform selector = FindDirectChild(card, selectors[i]);
                if (selector == null)
                {
                    continue;
                }

                ComposeSelectorRow(card, selectors[i], 0f, 0f, rightWidth, 58f, i == 0);
                PlaceInCard(selector, rightX, y, rightWidth, 58f);
                y += 74f;
            }

            Button start = FindDirectButton(card, "Start");
            if (start != null)
            {
                PulseForgeUIVisualStyle.ApplyButtonStyle(start, PulseForgeButtonStyle.Primary);
                PlaceInCard(start.GetComponent<RectTransform>(), rightX, 390f, rightWidth, 110f);
                Text label = start.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.fontSize = 40;
                    label.resizeTextMaxSize = 40;
                }
            }

            Button another = FindDirectButton(card, "Choose Another Song");
            Button change = FindDirectButton(card, "Change Settings");
            PulseForgeUIVisualStyle.ApplyButtonStyle(another, PulseForgeButtonStyle.Secondary);
            PulseForgeUIVisualStyle.ApplyButtonStyle(change, PulseForgeButtonStyle.Subtle);
            if (another != null)
            {
                PlaceInCard(another.GetComponent<RectTransform>(), leftX, 440f, 300f, 60f);
            }

            if (change != null)
            {
                PlaceInCard(change.GetComponent<RectTransform>(), leftX + 320f, 440f, 300f, 60f);
            }

            PlaceLeftovers(card, leftX, 380f, leftWidth);
        }

        // ---------------------------------------------------------------- results: the score is the hero

        private static void ComposeResults(PulseForgeSceneUIRoot root)
        {
            RectTransform card = FindCard(root.ResultsPanel, "Results Panel Card");
            if (card == null)
            {
                return;
            }

            PrepareStageCard(card, new Vector2(1200f, 660f));
            const float leftX = 60f;
            const float rightX = 660f;
            const float columnWidth = 480f;

            Text heading = FindDirectText(card, "Heading");
            if (heading != null)
            {
                StyleKicker(heading);
                heading.fontSize = 20;
                PlaceInCard(heading.rectTransform, leftX, 44f, columnWidth, 30f);
            }

            Text total = FindDirectText(card, "Total Score");
            if (total != null)
            {
                total.fontSize = 64;
                total.fontStyle = FontStyle.Bold;
                total.color = PulseForgeUITheme.Heat;
                total.alignment = TextAnchor.UpperLeft;
                total.resizeTextForBestFit = true;
                total.resizeTextMinSize = 28;
                total.resizeTextMaxSize = 64;
                PlaceInCard(total.rectTransform, leftX, 84f, columnWidth, 160f);
                Shadow glow = GetOrAdd<Shadow>(total.gameObject);
                glow.effectColor = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.EmberDeep, 0.6f);
                glow.effectDistance = new Vector2(0f, -4f);
            }

            Text combo = FindDirectText(card, "Max Combo");
            if (combo != null)
            {
                combo.fontSize = 26;
                combo.color = PulseForgeUITheme.PrimaryText;
                combo.alignment = TextAnchor.MiddleLeft;
                PlaceInCard(combo.rectTransform, leftX, 262f, columnWidth, 44f);
            }

            string[] tally = { "Perfect", "Good", "Miss" };
            for (int i = 0; i < tally.Length; i++)
            {
                Text text = FindDirectText(card, tally[i]);
                if (text != null)
                {
                    text.fontSize = 28;
                    text.fontStyle = FontStyle.Bold;
                    text.alignment = TextAnchor.MiddleLeft;
                    PlaceInCard(text.rectTransform, rightX, 72f + i * 58f, columnWidth, 50f);
                }
            }

            string[] run = { "Game Mode", "Remaining Health", "Failure" };
            for (int i = 0; i < run.Length; i++)
            {
                Text text = FindDirectText(card, run[i]);
                if (text != null)
                {
                    text.fontSize = 18;
                    text.color = PulseForgeUITheme.SecondaryText;
                    text.alignment = TextAnchor.MiddleLeft;
                    PlaceInCard(text.rectTransform, rightX, 262f + i * 40f, columnWidth, 36f);
                }
            }

            Button replay = FindDirectButton(card, "Replay");
            Button another = FindDirectButton(card, "Choose Another Song");
            PulseForgeUIVisualStyle.ApplyButtonStyle(replay, PulseForgeButtonStyle.Primary);
            PulseForgeUIVisualStyle.ApplyButtonStyle(another, PulseForgeButtonStyle.Secondary);
            if (replay != null)
            {
                PlaceInCard(replay.GetComponent<RectTransform>(), rightX, 520f, columnWidth, 92f);
                Text label = replay.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.fontSize = 34;
                    label.resizeTextMaxSize = 34;
                }
            }

            if (another != null)
            {
                PlaceInCard(another.GetComponent<RectTransform>(), leftX, 544f, columnWidth, 68f);
            }

            PlaceLeftovers(card, leftX, 330f, columnWidth);
        }

        private static readonly HashSet<string> SkinOwnedNames = new HashSet<string>
        {
            PulseForgeUIVisualStyle.SkinFrameName, AccentName, LoadoutHeaderName, LoadoutPanelName, TuningHeaderName, TitleAccentName
        };

        /// <summary>Stacks any direct child the composition did not name (e.g. status lines) into a free slot.</summary>
        private static void PlaceLeftovers(RectTransform card, float x, float top, float width)
        {
            float y = top;
            for (int i = 0; i < card.childCount; i++)
            {
                RectTransform child = card.GetChild(i) as RectTransform;
                if (child == null || SkinOwnedNames.Contains(child.gameObject.name) || Placed.Contains(child))
                {
                    continue;
                }

                float height = 0f;
                LayoutElement element = child.GetComponent<LayoutElement>();
                if (element != null)
                {
                    height = element.preferredHeight;
                }

                height = height > 0f ? Mathf.Min(height, 120f) : 60f;
                Text text = child.GetComponent<Text>();
                if (text != null)
                {
                    text.alignment = TextAnchor.UpperLeft;
                    text.fontSize = Mathf.Min(text.fontSize, 18);
                }

                PlaceInCard(child, x, y, width, height);
                y += height + 8f;
            }
        }

        private static void PrepareStageCard(RectTransform card, Vector2 size)
        {
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = size;
            card.anchoredPosition = Vector2.zero;
            LayoutGroup layout = card.GetComponent<LayoutGroup>();
            if (layout != null)
            {
                layout.enabled = false;
            }

            ContentSizeFitter fitter = card.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.enabled = false;
            }
        }

        private static void PlaceInCard(RectTransform rect, float x, float top, float width, float height)
        {
            if (rect == null)
            {
                return;
            }

            LayoutElement layoutElement = rect.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.ignoreLayout = true;
            }

            Placed.Add(rect);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -top);
        }

        private static RectTransform FindCard(PulseForgePanelView view, string cardName)
        {
            return view == null || view.PanelRoot == null ? null : view.PanelRoot.transform.Find(cardName) as RectTransform;
        }

        private static Text FindDirectText(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            return child == null ? null : child.GetComponent<Text>();
        }

        private static Button FindDirectButton(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            return child == null ? null : child.GetComponent<Button>();
        }

        // ---------------------------------------------------------------- HUD

        private static void StyleHud(PulseForgeSceneUIRoot root)
        {
            if (root.GameplayHud == null)
            {
                return;
            }

            RectTransform topBar = FindRect(root.GameplayHud.transform, "Top HUD");
            if (topBar != null)
            {
                StylePlate(topBar, PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Surface, 0.80f), PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Border, 0.9f), 1f);
                AddAccent(topBar, PulseForgeUITheme.Ember, new Vector2(30f, -8f), new Vector2(80f, 3f));
                RectTransform progress = FindRect(topBar, "Song Progress");
                Image fill = progress == null ? null : FindImage(progress, "Fill");
                if (fill != null)
                {
                    fill.color = PulseForgeUITheme.Ember;
                }

                Text score = FindText(topBar, "Score");
                if (score != null)
                {
                    score.fontSize = Mathf.Max(score.fontSize, 30);
                }
            }

            RectTransform lanes = FindRect(root.GameplayHud.transform, "Rhythm Lanes");
            if (lanes != null)
            {
                StylePlate(lanes, PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Surface, 0.66f), PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Border, 0.7f), 1f);
            }

            RhythmLaneView laneView = root.GameplayHud.RhythmLaneView;
            if (laneView != null)
            {
                StyleLaneRow(laneView.GuardLaneRoot);
                StyleLaneRow(laneView.StrikeLaneRoot);
            }

            StyleProcessingList(root);
        }

        private static void StyleLaneRow(RectTransform lane)
        {
            if (lane == null)
            {
                return;
            }

            StylePlate(lane, PulseForgeUITheme.WithAlpha(PulseForgeUITheme.SurfaceRaised, 0.62f), PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Border, 0.6f), 1.6f);
        }

        private static void StyleProcessingList(PulseForgeSceneUIRoot root)
        {
            RectTransform card = FindCard(root.ProcessingPanel, "Processing Panel Card");
            if (card == null)
            {
                return;
            }

            for (int i = 0; i < card.childCount; i++)
            {
                Text text = card.GetChild(i).GetComponent<Text>();
                if (text != null && text.gameObject.name != "Heading")
                {
                    text.alignment = TextAnchor.MiddleLeft;
                }
            }
        }

        // ---------------------------------------------------------------- helpers

        private static void StylePlate(RectTransform rect, Color fill, Color border, float pixelsPerUnitMultiplier)
        {
            if (rect == null)
            {
                return;
            }

            Image image = rect.GetComponent<Image>();
            if (image == null)
            {
                image = rect.gameObject.AddComponent<Image>();
            }

            image.sprite = PulseForgeSkinAssets.Plate;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            image.color = fill;
            DisableOutline(rect);
            PulseForgeUIVisualStyle.ApplyFrame(rect, border, pixelsPerUnitMultiplier);
        }

        private static void AddAccent(RectTransform parent, Color color, Vector2 offset, Vector2 size, bool leftEdge = false)
        {
            Transform existing = parent.Find(AccentName);
            RectTransform accent = existing as RectTransform;
            Image image;
            if (accent == null)
            {
                accent = PulseForgeUIFactory.CreateRect(AccentName, parent);
                image = accent.gameObject.AddComponent<Image>();
                accent.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }
            else
            {
                image = accent.GetComponent<Image>();
            }

            image.raycastTarget = false;
            image.color = color;
            if (leftEdge)
            {
                accent.anchorMin = new Vector2(0f, 0.22f);
                accent.anchorMax = new Vector2(0f, 0.78f);
                accent.pivot = new Vector2(0f, 0.5f);
                accent.anchoredPosition = offset;
                accent.sizeDelta = new Vector2(size.x, 0f);
            }
            else
            {
                accent.anchorMin = new Vector2(0f, 1f);
                accent.anchorMax = new Vector2(0f, 1f);
                accent.pivot = new Vector2(0f, 1f);
                accent.anchoredPosition = offset;
                accent.sizeDelta = size;
            }
        }

        private static void StyleKicker(Text text)
        {
            text.fontSize = 16;
            text.fontStyle = FontStyle.Bold;
            text.color = PulseForgeUITheme.Ember;
            text.alignment = TextAnchor.MiddleLeft;
            if (!string.IsNullOrEmpty(text.text))
            {
                text.text = text.text.ToUpperInvariant();
            }
        }

        private static void StyleSectionHeader(Text text, Color color, int size)
        {
            if (text == null)
            {
                return;
            }

            text.font = PulseForgeUIFactory.DefaultFont;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
        }

        private static RectTransform EnsureChild(RectTransform parent, string name, out Image image)
        {
            Transform existing = parent.Find(name);
            RectTransform rect = existing as RectTransform;
            if (rect == null)
            {
                rect = PulseForgeUIFactory.CreateRect(name, parent);
                rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }

            image = rect.GetComponent<Image>();
            if (image == null)
            {
                image = rect.gameObject.AddComponent<Image>();
            }

            image.raycastTarget = false;
            return rect;
        }

        private static RectTransform EnsureText(RectTransform parent, string name, string value)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing as RectTransform;
            }

            Text text = PulseForgeUIFactory.CreateText(name, parent, value, 20, PulseForgeUITheme.PrimaryText);
            text.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return text.rectTransform;
        }

        private static void PlaceLeft(RectTransform rect, float x, float top, float width, float height)
        {
            PlaceColumn(rect, 0f, x, top, width, height);
        }

        private static void PlaceRight(RectTransform rect, float x, float top, float width, float height)
        {
            PlaceColumn(rect, 1f, x, top, width, height);
        }

        // Positions in 1920x1080 reference space; columns hug their screen edge and centre vertically.
        private static void PlaceColumn(RectTransform rect, float side, float inset, float top, float width, float height)
        {
            if (rect == null)
            {
                return;
            }

            LayoutElement layoutElement = rect.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.ignoreLayout = true;
            }

            rect.anchorMin = new Vector2(side, 0.5f);
            rect.anchorMax = new Vector2(side, 0.5f);
            rect.pivot = new Vector2(side, 1f);
            rect.sizeDelta = new Vector2(width, height);
            float x = side < 0.5f ? inset : -inset;
            rect.anchoredPosition = new Vector2(x, PulseForgeUITheme.ReferenceHeight * 0.5f - top);
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Image AddImage(string name, Transform parent, Sprite sprite, Color color)
        {
            RectTransform rect = PulseForgeUIFactory.CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void DisableOutline(Component component)
        {
            Outline outline = component.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
            }
        }

        private static RectTransform FindDirectChild(Transform parent, string name)
        {
            return parent.Find(name) as RectTransform;
        }

        private static RectTransform FindRect(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            RectTransform[] rects = parent.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i].gameObject.name == name)
                {
                    return rects[i];
                }
            }

            return null;
        }

        private static Text FindText(Transform parent, string name)
        {
            RectTransform rect = FindRect(parent, name);
            return rect == null ? null : rect.GetComponent<Text>();
        }

        private static Button FindButton(Transform parent, string name)
        {
            RectTransform rect = FindRect(parent, name);
            return rect == null ? null : rect.GetComponent<Button>();
        }

        private static Image FindImage(Transform parent, string name)
        {
            RectTransform rect = FindRect(parent, name);
            return rect == null ? null : rect.GetComponent<Image>();
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component == null ? gameObject.AddComponent<T>() : component;
        }

        private static bool SameRgb(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.012f && Mathf.Abs(a.g - b.g) < 0.012f && Mathf.Abs(a.b - b.b) < 0.012f;
        }

        private static KeyValuePair<Color, Color> Pair(int r, int g, int b, Color replacement)
        {
            return new KeyValuePair<Color, Color>(new Color(r / 255f, g / 255f, b / 255f, 1f), replacement);
        }
    }
}
