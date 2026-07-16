using System;
using System.Collections.Generic;
using System.Globalization;
using PulseForge.Runtime.Unity.Input;
using PulseForge.Runtime.Unity.Persistence;
using PulseForge.Runtime.Unity.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class SettingsPanelView : PulseForgePanelView
    {
        [SerializeField] private Slider masterVolume;
        [SerializeField] private Slider musicVolume;
        [SerializeField] private Text masterValue;
        [SerializeField] private Text musicValue;
        [SerializeField] private Text displayModeValue;
        [SerializeField] private Text resolutionValue;
        [SerializeField] private Text frameRateValue;
        [SerializeField] private Toggle vSync;
        [SerializeField] private Text guardBinding;
        [SerializeField] private Text strikeBinding;
        [SerializeField] private Text dodgeBinding;
        [SerializeField] private Text heavyAttackBinding;
        [SerializeField] private Text pauseBinding;
        [SerializeField] private Toggle motion;
        [SerializeField] private Text detectionValue;
        [SerializeField] private Text difficultyValue;
        [SerializeField] private Text combatStyleValue;
        [SerializeField] private Text gameModeValue;
        [SerializeField] private InputField beatmapOffset;
        [SerializeField] private InputField inputOffset;
        [SerializeField] private Text message;
        [SerializeField] private GameObject rebindOverlay;
        [SerializeField] private Button applyButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button resetDefaultsButton;
        [SerializeField] private Button resetBindingsButton;
        [SerializeField] private Button cancelRebindButton;
        [SerializeField] private Button[] optionButtons;
        [SerializeField] private Button[] rebindButtons;

        private DebugRhythmPrototypeController controller;
        private int lastRevision = -1;

        public static SettingsPanelView Create(Transform parent)
        {
            RectTransform overlay = PulseForgeUIFactory.CreatePanel("Settings Overlay", parent, PulseForgeUITheme.Overlay);
            SettingsPanelView view = overlay.gameObject.AddComponent<SettingsPanelView>();
            view.ConfigurePanelRoot(overlay.gameObject);

            RectTransform card = PulseForgeUIFactory.CreateCenteredCard(
                "Settings Card", overlay, new Vector2(1080f, 900f), PulseForgeUITheme.SurfaceRaised);
            VerticalLayoutGroup cardLayout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            cardLayout.padding = new RectOffset(42, 42, 34, 34);
            cardLayout.spacing = 14f;
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = true;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;

            Text title = PulseForgeUIFactory.CreateText("Title", card, "SETTINGS", 34,
                PulseForgeUITheme.PrimaryText, TextAnchor.MiddleLeft, FontStyle.Bold);
            PulseForgeUIFactory.SetLayoutHeight(title, 52f);

            RectTransform scrollRoot = PulseForgeUIFactory.CreateRect("Settings Scroll", card);
            LayoutElement scrollLayout = PulseForgeUIFactory.SetLayoutHeight(scrollRoot, 690f);
            scrollLayout.flexibleHeight = 1f;
            Image scrollImage = scrollRoot.gameObject.AddComponent<Image>();
            scrollImage.color = PulseForgeUITheme.Surface;
            ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            RectTransform viewport = PulseForgeUIFactory.CreateStretchRect("Viewport", scrollRoot);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.clear;
            viewportImage.raycastTarget = true;
            RectTransform content = PulseForgeUIFactory.CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(28, 28, 20, 20);
            contentLayout.spacing = 10f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;

            AddSection(content, "AUDIO");
            view.masterVolume = AddSliderRow(content, "Master Volume", out view.masterValue);
            view.musicVolume = AddSliderRow(content, "Music Volume", out view.musicValue);

            List<Button> options = new List<Button>();
            AddSection(content, "DISPLAY");
            view.displayModeValue = AddOptionRow(content, "Display Mode", options);
            view.resolutionValue = AddOptionRow(content, "Resolution", options);
            view.vSync = AddToggleRow(content, "VSync");
            view.frameRateValue = AddOptionRow(content, "Frame Rate Limit", options);

            AddSection(content, "CONTROLS");
            List<Button> rebinds = new List<Button>();
            view.guardBinding = AddBindingRow(content, "Guard", rebinds);
            view.strikeBinding = AddBindingRow(content, "Light Attack", rebinds);
            view.dodgeBinding = AddBindingRow(content, "Dodge", rebinds);
            view.heavyAttackBinding = AddBindingRow(content, "Heavy Attack", rebinds);
            view.pauseBinding = AddBindingRow(content, "Pause", rebinds);
            view.resetBindingsButton = PulseForgeUIFactory.CreateButton(
                "Reset Bindings", content, "Reset Bindings", PulseForgeUITheme.SecondaryText);
            PulseForgeUIFactory.SetLayoutHeight(view.resetBindingsButton, 50f);

            AddSection(content, "GAMEPLAY");
            view.motion = AddToggleRow(content, "Motion Effects");
            view.detectionValue = AddOptionRow(content, "Default Detection", options);
            view.difficultyValue = AddOptionRow(content, "Default Difficulty", options);
            view.combatStyleValue = AddOptionRow(content, "Default Combat Style", options);
            view.gameModeValue = AddOptionRow(content, "Default Game Mode", options);
            view.beatmapOffset = AddInputRow(content, "Beatmap Offset (ms)");
            view.inputOffset = AddInputRow(content, "Input Timing Offset (ms)");
            view.optionButtons = options.ToArray();
            view.rebindButtons = rebinds.ToArray();

            view.message = PulseForgeUIFactory.CreateText("Message", card, string.Empty,
                PulseForgeUITheme.SmallFontSize, PulseForgeUITheme.SecondaryText, TextAnchor.MiddleLeft);
            PulseForgeUIFactory.SetLayoutHeight(view.message, 34f);

            RectTransform footer = PulseForgeUIFactory.CreateRect("Footer", card);
            PulseForgeUIFactory.SetLayoutHeight(footer, 58f);
            HorizontalLayoutGroup footerLayout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 12f;
            footerLayout.childControlWidth = true;
            footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = true;
            footerLayout.childForceExpandHeight = true;
            view.resetDefaultsButton = PulseForgeUIFactory.CreateButton(
                "Reset to Defaults", footer, "Reset to Defaults", PulseForgeUITheme.SecondaryText);
            view.cancelButton = PulseForgeUIFactory.CreateButton("Cancel", footer, "Cancel", PulseForgeUITheme.SecondaryText);
            view.applyButton = PulseForgeUIFactory.CreateButton("Apply", footer, "Apply", PulseForgeUITheme.Primary);

            RectTransform rebind = PulseForgeUIFactory.CreatePanel("Rebind Overlay", overlay, PulseForgeUITheme.Overlay);
            view.rebindOverlay = rebind.gameObject;
            RectTransform rebindCard = PulseForgeUIFactory.CreateCenteredCard(
                "Rebind Card", rebind, new Vector2(520f, 250f), PulseForgeUITheme.SurfaceRaised);
            Text prompt = PulseForgeUIFactory.CreateText("Prompt", rebindCard, "Press a key…", 30,
                PulseForgeUITheme.PrimaryText, TextAnchor.MiddleCenter, FontStyle.Bold);
            PulseForgeUIFactory.Stretch(prompt.rectTransform, 30f, 34f, 30f, 92f);
            view.cancelRebindButton = PulseForgeUIFactory.CreateButton(
                "Cancel Rebind", rebindCard, "Cancel", PulseForgeUITheme.SecondaryText);
            PulseForgeUIFactory.SetBottom(view.cancelRebindButton.GetComponent<RectTransform>(), 58f, 70f, 70f, 28f);
            view.rebindOverlay.SetActive(false);
            return view;
        }

        public void EnsureGameModeControl(Action<GameObject> registerCreated = null)
        {
            Transform content = PanelRoot == null
                ? null
                : PanelRoot.transform.Find("Settings Card/Settings Scroll/Viewport/Content");
            if (content == null) return;
            Transform row = content.Find("Default Game Mode Row");
            if (row == null)
            {
                List<Button> addedButtons = new List<Button>();
                gameModeValue = AddOptionRow(content, "Default Game Mode", addedButtons);
                row = content.Find("Default Game Mode Row");
                Transform offsetRow = content.Find("Beatmap Offset (ms) Row");
                if (row != null && offsetRow != null)
                {
                    row.SetSiblingIndex(offsetRow.GetSiblingIndex());
                }
                optionButtons = AppendButtons(optionButtons, addedButtons);
                if (row != null) registerCreated?.Invoke(row.gameObject);
            }
            else
            {
                Transform value = row.Find("Value");
                gameModeValue = value == null ? null : value.GetComponent<Text>();
                if (optionButtons == null || optionButtons.Length < 14)
                {
                    List<Button> addedButtons = new List<Button>(row.GetComponentsInChildren<Button>(true));
                    optionButtons = AppendButtons(optionButtons, addedButtons);
                }
            }
        }

        public void Bind(DebugRhythmPrototypeController value)
        {
            if (controller == value) return;
            Unbind();
            controller = value;
            if (controller == null) return;
            masterVolume.onValueChanged.AddListener(controller.SetDraftMasterVolume);
            musicVolume.onValueChanged.AddListener(controller.SetDraftMusicVolume);
            vSync.onValueChanged.AddListener(controller.SetDraftVSync);
            motion.onValueChanged.AddListener(controller.SetDraftMotion);
            beatmapOffset.onEndEdit.AddListener(controller.SetDraftBeatmapOffsetMilliseconds);
            inputOffset.onEndEdit.AddListener(controller.SetDraftInputOffsetMilliseconds);
            BindOptions();
            PulseForgeUIFactory.BindButton(rebindButtons[0], () => controller.BeginDraftRebind(PulseForgeInputAction.Guard));
            PulseForgeUIFactory.BindButton(rebindButtons[1], () => controller.BeginDraftRebind(PulseForgeInputAction.LightAttack));
            PulseForgeUIFactory.BindButton(rebindButtons[2], () => controller.BeginDraftRebind(PulseForgeInputAction.Dodge));
            PulseForgeUIFactory.BindButton(rebindButtons[3], () => controller.BeginDraftRebind(PulseForgeInputAction.HeavyAttack));
            PulseForgeUIFactory.BindButton(rebindButtons[4], () => controller.BeginDraftRebind(PulseForgeInputAction.Pause));
            PulseForgeUIFactory.BindButton(resetBindingsButton, controller.ResetDraftBindings);
            PulseForgeUIFactory.BindButton(cancelRebindButton, controller.CancelDraftRebind);
            PulseForgeUIFactory.BindButton(applyButton, controller.ApplySettingsDraft);
            PulseForgeUIFactory.BindButton(cancelButton, controller.CancelSettings);
            PulseForgeUIFactory.BindButton(resetDefaultsButton, controller.ResetSettingsDraftToDefaults);
        }

        public void Unbind()
        {
            masterVolume?.onValueChanged.RemoveAllListeners();
            musicVolume?.onValueChanged.RemoveAllListeners();
            vSync?.onValueChanged.RemoveAllListeners();
            motion?.onValueChanged.RemoveAllListeners();
            beatmapOffset?.onEndEdit.RemoveAllListeners();
            inputOffset?.onEndEdit.RemoveAllListeners();
            UnbindButtons(optionButtons);
            UnbindButtons(rebindButtons);
            PulseForgeUIFactory.UnbindButton(resetBindingsButton);
            PulseForgeUIFactory.UnbindButton(cancelRebindButton);
            PulseForgeUIFactory.UnbindButton(applyButton);
            PulseForgeUIFactory.UnbindButton(cancelButton);
            PulseForgeUIFactory.UnbindButton(resetDefaultsButton);
            controller = null;
            lastRevision = -1;
        }

        public void Refresh(DebugRhythmPrototypeController value)
        {
            if (value == null || value.SettingsDraft == null) return;
            rebindOverlay.SetActive(value.IsInputRebinding);
            if (lastRevision == value.SettingsDraftRevision) return;
            lastRevision = value.SettingsDraftRevision;
            PulseForgeSettingsData draft = value.SettingsDraft;
            masterVolume.SetValueWithoutNotify(draft.audio.masterVolume);
            musicVolume.SetValueWithoutNotify(draft.audio.musicVolume);
            masterValue.text = Mathf.RoundToInt(draft.audio.masterVolume * 100f) + "%";
            musicValue.text = Mathf.RoundToInt(draft.audio.musicVolume * 100f) + "%";
            displayModeValue.text = draft.display.displayMode;
            resolutionValue.text = draft.display.resolutionWidth + " × " + draft.display.resolutionHeight
                + " @ " + draft.display.refreshRate + " Hz";
            vSync.SetIsOnWithoutNotify(draft.display.vSync);
            frameRateValue.text = draft.display.frameRateLimit < 0 ? "Unlimited" : draft.display.frameRateLimit.ToString();
            guardBinding.text = value.GetDraftBindingDisplay(PulseForgeInputAction.Guard);
            strikeBinding.text = value.GetDraftBindingDisplay(PulseForgeInputAction.LightAttack);
            dodgeBinding.text = value.GetDraftBindingDisplay(PulseForgeInputAction.Dodge);
            heavyAttackBinding.text = value.GetDraftBindingDisplay(PulseForgeInputAction.HeavyAttack);
            pauseBinding.text = value.GetDraftBindingDisplay(PulseForgeInputAction.Pause);
            motion.SetIsOnWithoutNotify(draft.enableMotion);
            detectionValue.text = draft.defaultDetection;
            difficultyValue.text = draft.defaultDifficulty;
            combatStyleValue.text = draft.defaultCombatStyle;
            gameModeValue.text = draft.defaultGameMode == "OneLife" ? "One Life" : draft.defaultGameMode;
            beatmapOffset.SetTextWithoutNotify((draft.beatmapOffsetSeconds * 1000f).ToString("0", CultureInfo.InvariantCulture));
            inputOffset.SetTextWithoutNotify((draft.inputTimingOffsetSeconds * 1000f).ToString("0", CultureInfo.InvariantCulture));
            message.text = value.SettingsMessage;
        }

        public override void CollectValidationErrors(List<string> errors)
        {
            base.CollectValidationErrors(errors);
            PulseForgeUIValidation.AddMissing(errors, applyButton, "Settings: Apply button is missing.");
            PulseForgeUIValidation.AddMissing(errors, cancelButton, "Settings: Cancel button is missing.");
            PulseForgeUIValidation.AddMissing(errors, resetDefaultsButton, "Settings: Reset to Defaults button is missing.");
            PulseForgeUIValidation.AddMissing(errors, gameModeValue, "Settings: Default Game Mode is missing.");
        }

        private void BindOptions()
        {
            System.Action<int, System.Action<int>> pair = (start, action) =>
            {
                PulseForgeUIFactory.BindButton(optionButtons[start], () => action(-1));
                PulseForgeUIFactory.BindButton(optionButtons[start + 1], () => action(1));
            };
            pair(0, controller.CycleDraftDisplayMode);
            pair(2, controller.CycleDraftResolution);
            pair(4, controller.CycleDraftFrameRate);
            pair(6, controller.CycleDraftDetection);
            pair(8, controller.CycleDraftDifficulty);
            pair(10, controller.CycleDraftCombatStyle);
            pair(12, controller.CycleDraftGameMode);
        }

        private static Button[] AppendButtons(Button[] existing, List<Button> added)
        {
            List<Button> combined = new List<Button>(existing ?? System.Array.Empty<Button>());
            for (int i = 0; i < added.Count; i++)
            {
                if (added[i] != null && !combined.Contains(added[i])) combined.Add(added[i]);
            }
            return combined.ToArray();
        }

        private static void UnbindButtons(Button[] buttons)
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Length; i++) PulseForgeUIFactory.UnbindButton(buttons[i]);
        }

        private static void AddSection(Transform parent, string label)
        {
            Text text = PulseForgeUIFactory.CreateText(label, parent, label, 18,
                PulseForgeUITheme.Primary, TextAnchor.MiddleLeft, FontStyle.Bold);
            PulseForgeUIFactory.SetLayoutHeight(text, 36f);
        }

        private static Slider AddSliderRow(Transform parent, string label, out Text value)
        {
            RectTransform row = CreateRow(parent, label);
            AddRowLabel(row, label);
            RectTransform sliderRect = PulseForgeUIFactory.CreateRect("Slider", row);
            Slider slider = sliderRect.gameObject.AddComponent<Slider>();
            Image background = sliderRect.gameObject.AddComponent<Image>();
            background.color = PulseForgeUITheme.Border;
            RectTransform fill = PulseForgeUIFactory.CreateStretchRect("Fill", sliderRect);
            PulseForgeUIFactory.Stretch(fill, 3f);
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = PulseForgeUITheme.Primary;
            slider.fillRect = fill;
            slider.targetGraphic = background;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            value = PulseForgeUIFactory.CreateText("Value", row, "100%", 16,
                PulseForgeUITheme.PrimaryText, TextAnchor.MiddleRight);
            PulseForgeUIFactory.SetLayoutWidth(value, 72f);
            return slider;
        }

        private static Text AddOptionRow(Transform parent, string label, List<Button> buttons)
        {
            RectTransform row = CreateRow(parent, label);
            AddRowLabel(row, label);
            Button previous = PulseForgeUIFactory.CreateButton("Previous", row, "‹", PulseForgeUITheme.SecondaryText, 22);
            PulseForgeUIFactory.SetLayoutWidth(previous, 52f);
            Text value = PulseForgeUIFactory.CreateText("Value", row, string.Empty, 16,
                PulseForgeUITheme.PrimaryText, TextAnchor.MiddleCenter, FontStyle.Bold);
            PulseForgeUIFactory.SetLayoutWidth(value, 260f, 1f);
            Button next = PulseForgeUIFactory.CreateButton("Next", row, "›", PulseForgeUITheme.SecondaryText, 22);
            PulseForgeUIFactory.SetLayoutWidth(next, 52f);
            buttons.Add(previous);
            buttons.Add(next);
            return value;
        }

        private static Toggle AddToggleRow(Transform parent, string label)
        {
            RectTransform row = CreateRow(parent, label);
            AddRowLabel(row, label);
            Toggle toggle = row.gameObject.AddComponent<Toggle>();
            Image target = row.gameObject.AddComponent<Image>();
            target.color = PulseForgeUITheme.SurfaceRaised;
            RectTransform mark = PulseForgeUIFactory.CreateRect("Checkmark", row);
            PulseForgeUIFactory.SetLayoutWidth(mark, 44f);
            Image graphic = mark.gameObject.AddComponent<Image>();
            graphic.color = PulseForgeUITheme.Primary;
            toggle.targetGraphic = target;
            toggle.graphic = graphic;
            return toggle;
        }

        private static Text AddBindingRow(Transform parent, string label, List<Button> rebinds)
        {
            RectTransform row = CreateRow(parent, label);
            AddRowLabel(row, label);
            Text value = PulseForgeUIFactory.CreateText("Binding", row, string.Empty, 16,
                PulseForgeUITheme.PrimaryText, TextAnchor.MiddleCenter, FontStyle.Bold);
            Button button = PulseForgeUIFactory.CreateButton("Rebind", row, "Rebind", PulseForgeUITheme.SecondaryText, 15);
            PulseForgeUIFactory.SetLayoutWidth(button, 150f);
            rebinds.Add(button);
            return value;
        }

        private static InputField AddInputRow(Transform parent, string label)
        {
            RectTransform row = CreateRow(parent, label);
            AddRowLabel(row, label);
            RectTransform fieldRoot = PulseForgeUIFactory.CreateRect("Input", row);
            Image image = fieldRoot.gameObject.AddComponent<Image>();
            image.color = PulseForgeUITheme.SurfaceRaised;
            Text text = PulseForgeUIFactory.CreateText("Text", fieldRoot, "0", 16,
                PulseForgeUITheme.PrimaryText, TextAnchor.MiddleCenter);
            PulseForgeUIFactory.Stretch(text.rectTransform, 12f, 4f, 12f, 4f);
            InputField input = fieldRoot.gameObject.AddComponent<InputField>();
            input.textComponent = text;
            input.contentType = InputField.ContentType.DecimalNumber;
            input.targetGraphic = image;
            return input;
        }

        private static RectTransform CreateRow(Transform parent, string name)
        {
            RectTransform row = PulseForgeUIFactory.CreateRect(name + " Row", parent);
            PulseForgeUIFactory.SetLayoutHeight(row, 54f);
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return row;
        }

        private static void AddRowLabel(Transform row, string label)
        {
            Text text = PulseForgeUIFactory.CreateText("Label", row, label, 16,
                PulseForgeUITheme.SecondaryText, TextAnchor.MiddleLeft);
            PulseForgeUIFactory.SetLayoutWidth(text, 300f, 1f);
        }
    }
}
