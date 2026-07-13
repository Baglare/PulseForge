using System;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public static class PulseForgeUIVisualStyle
    {
        public const string BackgroundLayersName = "M8A Background Layers";

        public static void Apply(PulseForgeSceneUIRoot root)
        {
            if (root == null)
            {
                return;
            }

            StyleCanvas(root);
            StyleBackground(root.Background);
            StyleSetup(root.SetupPanel);
            StyleProcessing(root.ProcessingPanel);
            StyleReady(root.ReadyPanel);
            StyleGameplay(root.GameplayHud);
            StyleCountdown(root.CountdownOverlay);
            StylePause(root.PauseOverlay);
            StyleResults(root.ResultsPanel);
            StyleError(root.ErrorPanel);
        }

        internal static void ApplyButtonStyle(Button button, PulseForgeButtonStyle style)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = PulseForgeUIFactory.RoundedSprite;
            image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                image.color = PulseForgeUITheme.CreateButtonColors(style).normalColor;
                image.raycastTarget = true;
            }

            button.transition = Selectable.Transition.ColorTint;
            button.colors = PulseForgeUITheme.CreateButtonColors(style);
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            Outline outline = GetOrAdd<Outline>(button.gameObject);
            outline.useGraphicAlpha = true;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.effectColor = GetButtonBorderColor(style);
            outline.enabled = style != PulseForgeButtonStyle.Subtle;

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.fontSize = PulseForgeUITypography.MainButton;
                label.fontStyle = FontStyle.Bold;
                label.color = style == PulseForgeButtonStyle.Subtle
                    ? PulseForgeUITheme.SecondaryText
                    : PulseForgeUITheme.PrimaryText;
                label.alignment = TextAnchor.MiddleCenter;
            }
        }

        private static void StyleCanvas(PulseForgeSceneUIRoot root)
        {
            if (root.Canvas == null)
            {
                return;
            }

            CanvasScaler scaler = root.Canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(PulseForgeUITheme.ReferenceWidth, PulseForgeUITheme.ReferenceHeight);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
        }

        private static void StyleBackground(GameObject background)
        {
            if (background == null)
            {
                return;
            }

            Image image = background.GetComponent<Image>();
            if (image != null)
            {
                image.color = PulseForgeUITheme.Backdrop;
                image.raycastTarget = false;
            }

            Transform existing = background.transform.Find(BackgroundLayersName);
            RectTransform layers = existing as RectTransform;
            if (layers == null)
            {
                layers = PulseForgeUIFactory.CreateStretchRect(BackgroundLayersName, background.transform);
                layers.SetAsLastSibling();
                for (int i = 0; i < 7; i++)
                {
                    float y = 0.14f + i * 0.12f;
                    CreateLine(layers, "Horizontal Line " + (i + 1), y, PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Border, 0.18f));
                }

                CreateDiagonal(layers, "Forge Diagonal Left", new Vector2(-560f, 250f), 18f);
                CreateDiagonal(layers, "Forge Diagonal Right", new Vector2(560f, -250f), 18f);
            }

            Graphic[] graphics = layers.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                graphics[i].raycastTarget = false;
            }
        }

        private static void StyleSetup(SetupPanelView view)
        {
            if (view == null)
            {
                return;
            }

            StyleScreenRoot(view, false);
            RectTransform card = FindRect(view.transform, "Setup Card");
            StyleCard(card, new Vector2(980f, 860f), new RectOffset(52, 52, 30, 30), 8f);
            StyleText(FindText(card, "Title"), PulseForgeUITypography.AppTitle, PulseForgeUITheme.PrimaryText, TextAnchor.MiddleCenter, FontStyle.Bold, 64f);
            StyleText(FindText(card, "Description"), PulseForgeUITypography.Body, PulseForgeUITheme.SecondaryText, TextAnchor.MiddleCenter, FontStyle.Normal, 38f);
            StyleText(FindText(card, "Audio Source Label"), PulseForgeUITypography.SectionHeading, PulseForgeUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold, 24f);
            SetHeight(FindRect(card, "Audio Source Actions"), 58f);
            ApplyButtonStyle(FindButton(card, "Play Built-in Demo"), PulseForgeButtonStyle.Subtle);
            ApplyButtonStyle(FindButton(card, "Choose Custom Audio"), PulseForgeButtonStyle.Secondary);

            RectTransform selectedCard = FindRect(card, "Selected Audio Card");
            StyleSurface(selectedCard, PulseForgeUITheme.SurfaceRaised, PulseForgeUITheme.Border);
            SetHeight(selectedCard, 52f);
            StyleText(FindText(selectedCard, "Selected Audio"), PulseForgeUITypography.Body, PulseForgeUITheme.SecondaryText, TextAnchor.MiddleLeft, FontStyle.Normal, 52f);

            StyleSelector(card, "Detection Selector", 78f);
            StyleSelector(card, "Difficulty Selector", 78f);
            StyleSelector(card, "Combat Style Selector", 78f);
            RectTransform libraryActions = FindRect(card, "Library Actions");
            SetHeight(libraryActions, 48f);
            ApplyButtonStyle(FindButton(libraryActions, "Saved Tracks"), PulseForgeButtonStyle.Secondary);
            Toggle saveToggle = libraryActions == null
                ? null
                : libraryActions.GetComponentInChildren<Toggle>(true);
            if (saveToggle != null)
            {
                saveToggle.colors = PulseForgeUITheme.CreateButtonColors(PulseForgeButtonStyle.Secondary);
                Text toggleLabel = saveToggle.GetComponentInChildren<Text>(true);
                StyleText(
                    toggleLabel,
                    PulseForgeUITypography.Secondary,
                    PulseForgeUITheme.PrimaryText,
                    TextAnchor.MiddleLeft,
                    FontStyle.Normal,
                    48f);
            }

            Button analyze = FindButton(card, "Analyze Song");
            ApplyButtonStyle(analyze, PulseForgeButtonStyle.Primary);
            SetHeight(analyze == null ? null : analyze.GetComponent<RectTransform>(), PulseForgeUILayout.LargeButtonHeight);
            StyleText(FindText(card, "Setup Status"), PulseForgeUITypography.Secondary, PulseForgeUITheme.SecondaryText, TextAnchor.MiddleCenter, FontStyle.Normal, 26f);
        }

        private static void StyleProcessing(ProcessingPanelView view)
        {
            if (view == null)
            {
                return;
            }

            StyleScreenRoot(view, false);
            RectTransform card = FindRect(view.transform, "Processing Panel Card");
            StyleCard(card, new Vector2(720f, 660f), new RectOffset(58, 58, 46, 46), 10f);
            Text heading = FindText(card, "Heading");
            StyleText(heading, PulseForgeUITypography.ScreenHeading, PulseForgeUITheme.PrimaryText, TextAnchor.MiddleCenter, FontStyle.Bold, 58f);
            if (heading != null)
            {
                heading.text = "Preparing Your Track";
            }

            SetPreviewStageText(card, "Audio selected", "○   Track selected");
            SetPreviewStageText(card, "Converting to WAV", "○   Converting to WAV");
            SetPreviewStageText(card, "Loading converted audio", "○   Loading track");
            SetPreviewStageText(card, "Detecting rhythm", "○   Detecting rhythm");
            SetPreviewStageText(card, "Building combat sequence", "○   Building combat sequence");
            SetPreviewStageText(card, "Ready", "○   Ready to play");

            Text[] texts = card == null ? Array.Empty<Text>() : card.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i].gameObject.name == "Heading")
                {
                    continue;
                }

                texts[i].fontSize = PulseForgeUITypography.Body;
                texts[i].alignment = TextAnchor.MiddleLeft;
            }
        }

        private static void StyleReady(ReadyPanelView view)
        {
            if (view == null)
            {
                return;
            }

            StyleScreenRoot(view, false);
            RectTransform card = FindRect(view.transform, "Ready Panel Card");
            StyleCard(card, new Vector2(820f, 748f), new RectOffset(56, 56, 42, 42), 12f);
            Text heading = FindText(card, "Heading");
            StyleText(heading, PulseForgeUITypography.ScreenHeading, PulseForgeUITheme.PrimaryText, TextAnchor.MiddleCenter, FontStyle.Bold, 62f);
            if (heading != null)
            {
                heading.text = "Track Ready";
            }
            StyleText(FindText(card, "Song"), 22, PulseForgeUITheme.PrimaryText, TextAnchor.MiddleCenter, FontStyle.Bold, 72f);
            StyleReadyValue(card, "Events");
            StyleReadyValue(card, "Detection");
            StyleReadyValue(card, "Difficulty");
            StyleReadyValue(card, "Combat Style");
            Button start = FindButton(card, "Start");
            ApplyButtonStyle(start, PulseForgeButtonStyle.Primary);
            SetHeight(start == null ? null : start.GetComponent<RectTransform>(), 72f);
            ApplyButtonStyle(FindButton(card, "Change Settings"), PulseForgeButtonStyle.Secondary);
            ApplyButtonStyle(FindButton(card, "Choose Another Song"), PulseForgeButtonStyle.Subtle);
        }

        private static void StyleGameplay(GameplayHUDView view)
        {
            if (view == null)
            {
                return;
            }

            RectTransform topBar = FindRect(view.transform, "Top HUD");
            if (topBar != null)
            {
                PulseForgeUIFactory.SetTop(topBar, PulseForgeUILayout.TopHudHeight, 28f, 28f, 22f);
                StyleSurface(topBar, PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Surface, 0.86f), PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Border, 0.72f));
                Text score = FindText(topBar, "Score");
                Text combo = FindText(topBar, "Combo");
                StyleText(score, PulseForgeUITypography.HudValue, PulseForgeUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold, null);
                StyleText(combo, PulseForgeUITypography.HudValue, PulseForgeUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold, null);
                Text[] topTexts = topBar.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < topTexts.Length; i++)
                {
                    if (string.IsNullOrEmpty(topTexts[i].gameObject.name))
                    {
                        topTexts[i].fontSize = 22;
                        topTexts[i].color = PulseForgeUITheme.PrimaryText;
                    }
                }

                RectTransform progress = FindRect(topBar, "Song Progress");
                if (progress != null)
                {
                    progress.anchorMin = new Vector2(0.36f, 0.13f);
                    progress.anchorMax = new Vector2(0.79f, 0.19f);
                    Image progressImage = progress.GetComponent<Image>();
                    if (progressImage != null)
                    {
                        progressImage.color = PulseForgeUITheme.SurfaceSoft;
                        progressImage.sprite = PulseForgeUIFactory.RoundedSprite;
                progressImage.type = progressImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                    }

                    RectTransform fill = FindRect(progress, "Fill");
                    Image fillImage = fill == null ? null : fill.GetComponent<Image>();
                    if (fillImage != null)
                    {
                        fillImage.color = PulseForgeUITheme.Primary;
                        fillImage.sprite = PulseForgeUIFactory.RoundedSprite;
                fillImage.type = fillImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                    }
                }

                ApplyButtonStyle(FindButton(topBar, "Pause"), PulseForgeButtonStyle.Subtle);
            }

            Text feedback = FindText(view.transform, "Combat Feedback");
            StyleText(feedback, 52, PulseForgeUITheme.Perfect, TextAnchor.MiddleCenter, FontStyle.Bold, null);
            if (feedback != null)
            {
                Outline outline = GetOrAdd<Outline>(feedback.gameObject);
                outline.effectColor = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Backdrop, 0.94f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            RectTransform laneHud = FindRect(view.transform, "Rhythm Lanes");
            if (laneHud != null)
            {
                PulseForgeUIFactory.SetBottom(laneHud, PulseForgeUILayout.LaneHudHeight, 28f, 28f, 22f);
                StyleSurface(laneHud, PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Surface, 0.70f), PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Border, 0.56f));
            }

            RhythmLaneView lanes = view.RhythmLaneView;
            if (lanes != null)
            {
                StyleLane(lanes.GuardLaneRoot, lanes.GuardHitZone, PulseForgeUITheme.Guard, "GUARD", "SPACE", 116f);
                StyleLane(lanes.StrikeLaneRoot, lanes.StrikeHitZone, PulseForgeUITheme.Strike, "STRIKE", "J", 28f);
            }
        }

        private static void StyleCountdown(CountdownOverlayView view)
        {
            if (view == null)
            {
                return;
            }

            Text countdown = FindText(view.transform, "Countdown");
            StyleText(countdown, 136, PulseForgeUITheme.PrimaryText, TextAnchor.MiddleCenter, FontStyle.Bold, null);
            if (countdown != null)
            {
                Outline outline = GetOrAdd<Outline>(countdown.gameObject);
                outline.effectColor = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Backdrop, 0.96f);
                outline.effectDistance = new Vector2(3f, -3f);
            }
        }

        private static void StylePause(PauseMenuView view)
        {
            if (view == null)
            {
                return;
            }

            StyleScreenRoot(view, true);
            RectTransform card = FindRect(view.transform, "Pause Card");
            StyleCard(card, new Vector2(600f, 470f), new RectOffset(52, 52, 42, 42), 14f);
            StyleText(FindText(card, "Title"), PulseForgeUITypography.ScreenHeading, PulseForgeUITheme.PrimaryText, TextAnchor.MiddleCenter, FontStyle.Bold, 72f);
            ApplyButtonStyle(FindButton(card, "Resume"), PulseForgeButtonStyle.Primary);
            ApplyButtonStyle(FindButton(card, "Restart Track"), PulseForgeButtonStyle.Secondary);
            ApplyButtonStyle(FindButton(card, "Change Song"), PulseForgeButtonStyle.Subtle);
        }

        private static void StyleResults(ResultsPanelView view)
        {
            if (view == null)
            {
                return;
            }

            StyleScreenRoot(view, false);
            RectTransform card = FindRect(view.transform, "Results Panel Card");
            StyleCard(card, new Vector2(760f, 720f), new RectOffset(56, 56, 36, 36), 9f);
            Text heading = FindText(card, "Heading");
            StyleText(heading, PulseForgeUITypography.ScreenHeading, PulseForgeUITheme.PrimaryText, TextAnchor.MiddleCenter, FontStyle.Bold, 62f);
            if (heading != null)
            {
                heading.text = "Track Complete";
            }
            StyleText(FindText(card, "Total Score"), 34, PulseForgeUITheme.PrimaryText, TextAnchor.MiddleCenter, FontStyle.Bold, 82f);
            StyleText(FindText(card, "Max Combo"), 20, PulseForgeUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold, 46f);
            StyleText(FindText(card, "Perfect"), 18, PulseForgeUITheme.Perfect, TextAnchor.MiddleLeft, FontStyle.Bold, 42f);
            StyleText(FindText(card, "Good"), 18, PulseForgeUITheme.Good, TextAnchor.MiddleLeft, FontStyle.Bold, 42f);
            StyleText(FindText(card, "Miss"), 18, PulseForgeUITheme.Miss, TextAnchor.MiddleLeft, FontStyle.Bold, 42f);
            ApplyButtonStyle(FindButton(card, "Replay"), PulseForgeButtonStyle.Primary);
            ApplyButtonStyle(FindButton(card, "Choose Another Song"), PulseForgeButtonStyle.Subtle);
        }

        private static void StyleError(ErrorPanelView view)
        {
            if (view == null)
            {
                return;
            }

            StyleScreenRoot(view, false);
            RectTransform card = FindRect(view.transform, "Error Panel Card");
            StyleCard(card, new Vector2(720f, 500f), new RectOffset(54, 54, 42, 42), 14f);
            StyleText(FindText(card, "Heading"), PulseForgeUITypography.ScreenHeading, PulseForgeUITheme.Miss, TextAnchor.MiddleCenter, FontStyle.Bold, 68f);
            ApplyButtonStyle(FindButton(card, "Retry"), PulseForgeButtonStyle.Primary);
            ApplyButtonStyle(FindButton(card, "Back to Setup"), PulseForgeButtonStyle.Subtle);
        }

        private static void StyleSelector(RectTransform card, string selectorName, float height)
        {
            RectTransform selector = FindRect(card, selectorName);
            SetHeight(selector, height);
            if (selector == null)
            {
                return;
            }

            VerticalLayoutGroup vertical = selector.GetComponent<VerticalLayoutGroup>();
            if (vertical != null)
            {
                vertical.spacing = 5f;
            }

            StyleText(FindText(selector, "Label"), PulseForgeUITypography.SectionHeading, PulseForgeUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold, 23f);
            RectTransform options = FindRect(selector, "Options");
            SetHeight(options, 48f);
            HorizontalLayoutGroup horizontal = options == null ? null : options.GetComponent<HorizontalLayoutGroup>();
            if (horizontal != null)
            {
                horizontal.spacing = 6f;
            }

            Button[] buttons = selector.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                ApplyButtonStyle(buttons[i], PulseForgeButtonStyle.Segment);
                Text label = buttons[i].GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.fontSize = PulseForgeUITypography.Body;
                }
            }
        }

        private static void StyleReadyValue(RectTransform card, string name)
        {
            StyleText(FindText(card, name), PulseForgeUITypography.Body, PulseForgeUITheme.SecondaryText, TextAnchor.MiddleLeft, FontStyle.Bold, 46f);
        }

        private static void SetPreviewStageText(RectTransform card, string objectName, string value)
        {
            Text text = FindText(card, objectName);
            if (text != null)
            {
                text.text = value;
                text.color = PulseForgeUITheme.SecondaryText;
            }
        }

        private static void StyleLane(RectTransform lane, RectTransform hitZone, Color accent, string action, string key, float bottom)
        {
            if (lane == null)
            {
                return;
            }

            lane.anchorMin = new Vector2(0f, 0f);
            lane.anchorMax = new Vector2(1f, 0f);
            lane.pivot = new Vector2(0.5f, 0f);
            lane.offsetMin = new Vector2(24f, bottom);
            lane.offsetMax = new Vector2(-24f, bottom + PulseForgeUILayout.LaneHeight);
            StyleSurface(lane, PulseForgeUITheme.WithAlpha(PulseForgeUITheme.SurfaceRaised, 0.38f), PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Border, 0.76f));

            RectTransform labelArea = FindRect(lane, "Lane Label");
            if (labelArea != null)
            {
                labelArea.offsetMin = new Vector2(18f, 8f);
                labelArea.offsetMax = new Vector2(176f, -8f);
                Text actionText = FindText(labelArea, "Action");
                StyleText(actionText, 19, accent, TextAnchor.MiddleLeft, FontStyle.Bold, null);
                if (actionText != null)
                {
                    actionText.text = action;
                    actionText.rectTransform.anchorMax = new Vector2(0.62f, 1f);
                }

                Text keyText = FindText(labelArea, "Key Hint");
                StyleText(keyText, PulseForgeUITypography.Secondary, PulseForgeUITheme.SecondaryText, TextAnchor.MiddleRight, FontStyle.Bold, null);
                if (keyText != null)
                {
                    keyText.text = key;
                    keyText.rectTransform.anchorMin = new Vector2(0.64f, 0f);
                }
            }

            RectTransform noteContainer = FindRect(lane, "Note Container");
            if (noteContainer != null)
            {
                noteContainer.offsetMin = new Vector2(190f, 7f);
                noteContainer.offsetMax = new Vector2(-18f, -7f);
                Image baseline = FindImage(noteContainer, "Baseline");
                if (baseline != null)
                {
                    baseline.color = PulseForgeUITheme.WithAlpha(accent, 0.26f);
                }
            }

            if (hitZone != null)
            {
                hitZone.sizeDelta = new Vector2(PulseForgeUILayout.HitZoneWidth, 0f);
                Image hitImage = hitZone.GetComponent<Image>();
                if (hitImage != null)
                {
                    hitImage.color = PulseForgeUITheme.WithAlpha(accent, 0.96f);
                }
            }
        }

        private static void StyleScreenRoot(PulseForgePanelView view, bool overlay)
        {
            if (view.PanelRoot == null)
            {
                return;
            }

            Image image = view.PanelRoot.GetComponent<Image>();
            if (image != null)
            {
                image.color = overlay
                    ? PulseForgeUITheme.Overlay
                    : PulseForgeUITheme.WithAlpha(PulseForgeUITheme.BackgroundSecondary, 0.72f);
            }
        }

        private static void StyleCard(RectTransform card, Vector2 size, RectOffset padding, float spacing)
        {
            if (card == null)
            {
                return;
            }

            card.sizeDelta = size;
            StyleSurface(card, PulseForgeUITheme.Surface, PulseForgeUITheme.Border);
            VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.padding = padding;
                layout.spacing = spacing;
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
            }
        }

        private static void StyleSurface(RectTransform rect, Color color, Color borderColor)
        {
            if (rect == null)
            {
                return;
            }

            Image image = rect.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
                image.sprite = PulseForgeUIFactory.RoundedSprite;
            image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            }

            Outline outline = GetOrAdd<Outline>(rect.gameObject);
            outline.useGraphicAlpha = true;
            outline.effectColor = borderColor;
            outline.effectDistance = new Vector2(PulseForgeUILayout.CardBorderThickness, -PulseForgeUILayout.CardBorderThickness);
        }

        private static void StyleText(Text text, int fontSize, Color color, TextAnchor alignment, FontStyle style, float? height)
        {
            if (text == null)
            {
                return;
            }

            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            if (height.HasValue)
            {
                SetHeight(text.rectTransform, height.Value);
            }
        }

        private static void SetHeight(RectTransform rect, float height)
        {
            if (rect != null)
            {
                PulseForgeUIFactory.SetLayoutHeight(rect, height);
            }
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

        private static Color GetButtonBorderColor(PulseForgeButtonStyle style)
        {
            switch (style)
            {
                case PulseForgeButtonStyle.Primary:
                case PulseForgeButtonStyle.SelectedSegment:
                    return PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Primary, 0.92f);
                case PulseForgeButtonStyle.Danger:
                    return PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Miss, 0.92f);
                case PulseForgeButtonStyle.Subtle:
                    return Color.clear;
                default:
                    return PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Border, 0.94f);
            }
        }

        private static void CreateLine(Transform parent, string name, float y, Color color)
        {
            RectTransform line = PulseForgeUIFactory.CreateRect(name, parent);
            line.anchorMin = new Vector2(0.06f, y);
            line.anchorMax = new Vector2(0.94f, y);
            line.pivot = new Vector2(0.5f, 0.5f);
            line.sizeDelta = new Vector2(0f, 1f);
            Image image = line.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void CreateDiagonal(Transform parent, string name, Vector2 position, float rotation)
        {
            RectTransform line = PulseForgeUIFactory.CreateRect(name, parent);
            line.anchorMin = new Vector2(0.5f, 0.5f);
            line.anchorMax = new Vector2(0.5f, 0.5f);
            line.pivot = new Vector2(0.5f, 0.5f);
            line.anchoredPosition = position;
            line.sizeDelta = new Vector2(720f, 2f);
            line.localRotation = Quaternion.Euler(0f, 0f, rotation);
            Image image = line.gameObject.AddComponent<Image>();
            image.color = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Primary, 0.10f);
            image.raycastTarget = false;
        }
    }
}
