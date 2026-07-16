using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public static class PulseForgeUIFactory
    {
        private static Font defaultFont;
        private static Sprite roundedSprite;

        internal static Sprite RoundedSprite => GetRoundedSprite();
        internal static Font DefaultFont => GetDefaultFont();

        public static PulseForgeSceneUIRoot CreateStaticHierarchy(
            Transform parent = null,
            string rootName = "PulseForge UI")
        {
            GameObject rootObject = new GameObject(
                rootName,
                typeof(RectTransform),
                typeof(PulseForgeSceneUIRoot),
                typeof(PulseForgeUIController));
            rootObject.transform.SetParent(parent, false);

            RectTransform canvasRect = CreateStretchRect("Canvas", rootObject.transform);
            Canvas canvas = canvasRect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler canvasScaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(
                PulseForgeUITheme.ReferenceWidth,
                PulseForgeUITheme.ReferenceHeight);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;
            canvasRect.gameObject.AddComponent<GraphicRaycaster>();

            RectTransform backgroundRect = CreatePanel("Background", canvasRect, PulseForgeUITheme.Backdrop);
            Image backgroundImage = backgroundRect.GetComponent<Image>();
            backgroundImage.raycastTarget = false;

            SetupPanelView setup = SetupPanelView.Create(canvasRect);
            SavedTracksPanelView savedTracks = SavedTracksPanelView.Create(canvasRect);
            ProcessingPanelView processing = ProcessingPanelView.Create(canvasRect);
            ReadyPanelView ready = ReadyPanelView.Create(canvasRect);
            GameplayHUDView gameplay = GameplayHUDView.Create(canvasRect);
            CountdownOverlayView countdown = CountdownOverlayView.Create(canvasRect);
            PauseMenuView pause = PauseMenuView.Create(canvasRect);
            ResultsPanelView results = ResultsPanelView.Create(canvasRect);
            ErrorPanelView error = ErrorPanelView.Create(canvasRect);

            PulseForgeSceneUIRoot root = rootObject.GetComponent<PulseForgeSceneUIRoot>();
            root.Configure(
                canvas,
                backgroundRect.gameObject,
                setup,
                processing,
                ready,
                gameplay,
                countdown,
                pause,
                results,
                error);
            root.ConfigureSavedTracksPanel(savedTracks);
            PulseForgePersistenceUISetup.Apply(root);
            PulseForgeSettingsUISetup.Apply(root);
            PulseForgeUIVisualStyle.Apply(root);
            PulseForgeUIMotionSetup.Apply(root);
            PulseForgeGameplayFeedbackSetup.Apply(root);
            RadialSaboteurFogSetup.Apply(root);
            PulseForgeGameModesUISetup.Apply(root);
            PulseForgeCoverageUISetup.Apply(root);
            PulseForgePlayabilityAssistUISetup.Apply(root);
            RadialForecastSetup.Apply(root);
            RadialGroupTimingSetup.Apply(root);
            root.ApplyVisibility(PulseForgeUIState.Setup);
            return root;
        }

        public static EventSystem CreateEventSystem(Transform parent = null)
        {
            GameObject eventSystemObject = new GameObject(
                "PulseForge EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystemObject.transform.SetParent(parent, false);
            InputSystemUIInputModule inputModule = eventSystemObject.GetComponent<InputSystemUIInputModule>();
            if (inputModule.actionsAsset == null)
            {
                inputModule.AssignDefaultActions();
            }

            return eventSystemObject.GetComponent<EventSystem>();
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            return rectTransform;
        }

        public static RectTransform CreateStretchRect(string name, Transform parent)
        {
            RectTransform rectTransform = CreateRect(name, parent);
            Stretch(rectTransform);
            return rectTransform;
        }

        public static RectTransform CreatePanel(string name, Transform parent, Color color)
        {
            RectTransform rectTransform = CreateStretchRect(name, parent);
            Image image = rectTransform.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return rectTransform;
        }

        public static RectTransform CreateCenteredCard(
            string name,
            Transform parent,
            Vector2 size,
            Color color)
        {
            RectTransform rectTransform = CreateRect(name, parent);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = Vector2.zero;
            Image image = rectTransform.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = RoundedSprite;
            image.type = RoundedSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            return rectTransform;
        }

        public static Text CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft,
            FontStyle fontStyle = FontStyle.Normal)
        {
            RectTransform rectTransform = CreateStretchRect(name, parent);
            Text text = rectTransform.gameObject.AddComponent<Text>();
            text.font = GetDefaultFont();
            text.text = value ?? string.Empty;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Color accent,
            int fontSize = PulseForgeUITheme.BodyFontSize)
        {
            RectTransform rectTransform = CreateStretchRect(name, parent);
            Image image = rectTransform.gameObject.AddComponent<Image>();
            image.color = PulseForgeUITheme.CreateButtonColors(accent).normalColor;

            Button button = rectTransform.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = PulseForgeUITheme.CreateButtonColors(accent);
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            Text buttonText = CreateText(
                "Label",
                rectTransform,
                label,
                fontSize,
                PulseForgeUITheme.PrimaryText,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            Stretch(buttonText.rectTransform, 12f, 8f, 12f, 8f);
            PulseForgeUIVisualStyle.ApplyButtonStyle(button, PulseForgeUITheme.ResolveButtonStyle(accent));
            return button;
        }

        public static void BindButton(Button button, UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            button.onClick.AddListener(ClearEventSystemSelection);
        }

        public static void UnbindButton(Button button)
        {
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
            }
        }

        public static LayoutElement SetLayoutHeight(Component component, float height, float flexibleWidth = 1f)
        {
            LayoutElement layoutElement = component.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = component.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
            layoutElement.flexibleHeight = 0f;
            layoutElement.flexibleWidth = flexibleWidth;
            return layoutElement;
        }

        public static LayoutElement SetLayoutWidth(Component component, float width, float flexibleWidth = 0f)
        {
            LayoutElement layoutElement = component.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = component.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.minWidth = width;
            layoutElement.preferredWidth = width;
            layoutElement.flexibleWidth = flexibleWidth;
            return layoutElement;
        }

        public static void Stretch(RectTransform rectTransform, float inset = 0f)
        {
            Stretch(rectTransform, inset, inset, inset, inset);
        }

        public static void Stretch(
            RectTransform rectTransform,
            float left,
            float top,
            float right,
            float bottom)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = new Vector2(left, bottom);
            rectTransform.offsetMax = new Vector2(-right, -top);
        }

        public static void SetTop(RectTransform rectTransform, float height, float left, float right, float top)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.offsetMin = new Vector2(left, -top - height);
            rectTransform.offsetMax = new Vector2(-right, -top);
        }

        public static void SetBottom(RectTransform rectTransform, float height, float left, float right, float bottom)
        {
            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 0f);
            rectTransform.pivot = new Vector2(0.5f, 0f);
            rectTransform.offsetMin = new Vector2(left, bottom);
            rectTransform.offsetMax = new Vector2(-right, bottom + height);
        }

        private static Font GetDefaultFont()
        {
            if (defaultFont != null)
            {
                return defaultFont;
            }

            defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null)
            {
                defaultFont = Font.CreateDynamicFontFromOSFont("Arial", PulseForgeUITheme.BodyFontSize);
            }

            return defaultFont;
        }

        private static Sprite GetRoundedSprite()
        {
            if (roundedSprite != null)
            {
                return roundedSprite;
            }

            // Unity 6 no longer exposes the legacy UGUI skin sprite at the old built-in path.
            // Keep the style asset-free and fall back to Image + Outline/Shadow instead.
            roundedSprite = null;
            return roundedSprite;
        }

        private static void ClearEventSystemSelection()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }
}
