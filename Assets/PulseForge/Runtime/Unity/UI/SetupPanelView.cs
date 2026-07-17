using System;
using System.Collections.Generic;
using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.Audio;
using PulseForge.Runtime.Unity.Persistence;
using PulseForge.Runtime.Unity.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class SetupPanelView : PulseForgePanelView
    {
        [SerializeField] private Button builtInDemoButton;
        [SerializeField] private Button chooseAudioButton;
        [SerializeField] private Button analyzeButton;
        [SerializeField] private Text selectedFileText;
        [SerializeField] private Text statusText;
        [SerializeField] private Toggle saveToLibraryToggle;
        [SerializeField] private Button savedTracksButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button[] detectionButtons;
        [SerializeField] private Button[] difficultyButtons;
        [SerializeField] private Button[] combatStyleButtons;
        [SerializeField] private Button[] coverageButtons;
        [SerializeField] private Button[] gameModeButtons;
        [SerializeField] private Button[] timingAssistButtons;

        private DebugRhythmPrototypeController boundController;
        private ChoiceSelector<RuntimeDetectionMode> detectionSelector;
        private ChoiceSelector<RuntimeDifficulty> difficultySelector;
        private ChoiceSelector<RuntimeCombatStyle> combatStyleSelector;
        private ChoiceSelector<RuntimeCoverage> coverageSelector;
        private ChoiceSelector<RadialGameMode> gameModeSelector;
        private ChoiceSelector<TimingAssistMode> timingAssistSelector;

        public Button BuiltInDemoButton => builtInDemoButton;
        public Button ChooseAudioButton => chooseAudioButton;
        public Button AnalyzeButton => analyzeButton;
        public Text SelectedFileText => selectedFileText;
        public Toggle SaveToLibraryToggle => saveToLibraryToggle;
        public Button SavedTracksButton => savedTracksButton;
        public Button SettingsButton => settingsButton;

        public static SetupPanelView Create(Transform parent)
        {
            RectTransform rootRect = PulseForgeUIFactory.CreatePanel("Setup Panel", parent, PulseForgeUITheme.Backdrop);
            SetupPanelView view = rootRect.gameObject.AddComponent<SetupPanelView>();
            view.ConfigurePanelRoot(rootRect.gameObject);

            RectTransform card = PulseForgeUIFactory.CreateCenteredCard(
                "Setup Card",
                rootRect,
                new Vector2(920f, 900f),
                PulseForgeUITheme.Surface);
            VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(48, 48, 42, 42);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Text title = PulseForgeUIFactory.CreateText(
                "Title",
                card,
                "PulseForge",
                PulseForgeUITheme.TitleFontSize,
                PulseForgeUITheme.PrimaryText,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            PulseForgeUIFactory.SetLayoutHeight(title, 76f);

            Text description = PulseForgeUIFactory.CreateText(
                "Description",
                card,
                "Turn a song into a playable rhythm-combat session.",
                PulseForgeUITheme.BodyFontSize,
                PulseForgeUITheme.SecondaryText,
                TextAnchor.MiddleCenter);
            PulseForgeUIFactory.SetLayoutHeight(description, 54f);

            Text audioSourceLabel = PulseForgeUIFactory.CreateText(
                "Audio Source Label",
                card,
                "Audio Source",
                PulseForgeUITheme.SmallFontSize,
                PulseForgeUITheme.SecondaryText,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            PulseForgeUIFactory.SetLayoutHeight(audioSourceLabel, 28f);

            RectTransform sourceButtons = PulseForgeUIFactory.CreateRect("Audio Source Actions", card);
            PulseForgeUIFactory.SetLayoutHeight(sourceButtons, PulseForgeUITheme.ButtonHeight);
            HorizontalLayoutGroup sourceLayout = sourceButtons.gameObject.AddComponent<HorizontalLayoutGroup>();
            sourceLayout.spacing = 10f;
            sourceLayout.childControlWidth = true;
            sourceLayout.childControlHeight = true;
            sourceLayout.childForceExpandWidth = true;
            sourceLayout.childForceExpandHeight = true;

            view.builtInDemoButton = PulseForgeUIFactory.CreateButton(
                "Play Built-in Demo",
                sourceButtons,
                "Play Built-in Demo",
                PulseForgeUITheme.SecondaryText);
            view.chooseAudioButton = PulseForgeUIFactory.CreateButton(
                "Choose Custom Audio",
                sourceButtons,
                "Choose Custom Audio",
                PulseForgeUITheme.Primary);

            RectTransform selectedAudioCard = PulseForgeUIFactory.CreateRect("Selected Audio Card", card);
            PulseForgeUIFactory.SetLayoutHeight(selectedAudioCard, 48f);
            Image selectedAudioBackground = selectedAudioCard.gameObject.AddComponent<Image>();
            selectedAudioBackground.color = PulseForgeUITheme.SurfaceRaised;

            view.selectedFileText = PulseForgeUIFactory.CreateText(
                "Selected Audio",
                selectedAudioCard,
                "No custom audio selected",
                PulseForgeUITheme.SmallFontSize,
                PulseForgeUITheme.SecondaryText,
                TextAnchor.MiddleLeft);
            PulseForgeUIFactory.Stretch(view.selectedFileText.rectTransform, 18f, 6f, 18f, 6f);

            view.detectionButtons = CreateChoiceButtons(
                card,
                "Detection",
                new[] { RuntimeDetectionMode.Onset.ToString(), RuntimeDetectionMode.Amplitude.ToString() });
            view.difficultyButtons = CreateChoiceButtons(
                card,
                "Difficulty",
                new[] { RuntimeDifficulty.Easy.ToString(), RuntimeDifficulty.Normal.ToString(), RuntimeDifficulty.Hard.ToString() });
            view.combatStyleButtons = CreateChoiceButtons(
                card,
                "Combat Style",
                new[]
                {
                    RuntimeCombatStyle.Legacy.ToString(),
                    RuntimeCombatStyle.Balanced.ToString(),
                    RuntimeCombatStyle.Defensive.ToString(),
                    RuntimeCombatStyle.Aggressive.ToString(),
                    RuntimeCombatStyle.Bursty.ToString()
                },
                18);
            view.coverageButtons = CreateChoiceButtons(
                card,
                "Coverage",
                new[] { "Relaxed", "Standard", "Full Pulse" });
            view.gameModeButtons = CreateChoiceButtons(
                card,
                "Game Mode",
                new[] { "Standard", "Survival", "One Life" });
            view.timingAssistButtons = CreateChoiceButtons(
                card,
                "Timing Assist",
                new[] { "Standard", "Relaxed", "Practice" });

            view.analyzeButton = PulseForgeUIFactory.CreateButton(
                "Analyze Song",
                card,
                "Analyze Song",
                PulseForgeUITheme.Primary,
                28);
            PulseForgeUIFactory.SetLayoutHeight(view.analyzeButton, PulseForgeUITheme.LargeButtonHeight);

            view.statusText = PulseForgeUIFactory.CreateText(
                "Setup Status",
                card,
                string.Empty,
                18,
                PulseForgeUITheme.SecondaryText,
                TextAnchor.MiddleCenter);
            PulseForgeUIFactory.SetLayoutHeight(view.statusText, 32f);
            view.EnsurePersistenceControls();
            view.EnsureSettingsButton();
            return view;
        }

        public void EnsureViewportLayout()
        {
            RectTransform card = PanelRoot == null
                ? null
                : PanelRoot.transform.Find("Setup Card") as RectTransform;
            if (card == null) return;
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = new Vector2(0f, -44f);
        }

        public void EnsureGameModeControls(Action<GameObject> registerCreated = null)
        {
            Transform card = PanelRoot == null ? null : PanelRoot.transform.Find("Setup Card");
            if (card == null) return;
            Transform selector = card.Find("Game Mode Selector");
            if (selector == null)
            {
                gameModeButtons = CreateChoiceButtons(
                    card,
                    "Game Mode",
                    new[] { "Standard", "Survival", "One Life" });
                selector = card.Find("Game Mode Selector");
                Transform combatStyle = card.Find("Combat Style Selector");
                if (selector != null && combatStyle != null)
                {
                    selector.SetSiblingIndex(combatStyle.GetSiblingIndex() + 1);
                }
                if (selector != null) registerCreated?.Invoke(selector.gameObject);
            }
            else
            {
                gameModeButtons = selector.GetComponentsInChildren<Button>(true);
            }

            RectTransform cardRect = card as RectTransform;
            if (cardRect != null && cardRect.sizeDelta.y < 1040f)
            {
                cardRect.sizeDelta = new Vector2(cardRect.sizeDelta.x, 1040f);
            }
        }

        public void EnsureCoverageControls(Action<GameObject> registerCreated = null)
        {
            Transform card = PanelRoot == null ? null : PanelRoot.transform.Find("Setup Card");
            if (card == null) return;
            Transform selector = card.Find("Coverage Selector");
            if (selector == null)
            {
                coverageButtons = CreateChoiceButtons(
                    card,
                    "Coverage",
                    new[] { "Relaxed", "Standard", "Full Pulse" });
                selector = card.Find("Coverage Selector");
                Transform difficulty = card.Find("Difficulty Selector");
                if (selector != null && difficulty != null)
                {
                    selector.SetSiblingIndex(difficulty.GetSiblingIndex() + 1);
                    registerCreated?.Invoke(selector.gameObject);
                }
            }
            else
            {
                coverageButtons = selector.GetComponentsInChildren<Button>(true);
            }

            RectTransform cardRect = card as RectTransform;
            if (cardRect != null && cardRect.sizeDelta.y < 1120f)
            {
                cardRect.sizeDelta = new Vector2(cardRect.sizeDelta.x, 1120f);
            }
        }

        public void EnsureTimingAssistControls(Action<GameObject> registerCreated = null)
        {
            Transform card = PanelRoot == null ? null : PanelRoot.transform.Find("Setup Card");
            if (card == null) return;
            Transform selector = card.Find("Timing Assist Selector");
            if (selector == null)
            {
                timingAssistButtons = CreateChoiceButtons(
                    card,
                    "Timing Assist",
                    new[] { "Standard", "Relaxed", "Practice" });
                selector = card.Find("Timing Assist Selector");
                Transform gameMode = card.Find("Game Mode Selector");
                if (selector != null && gameMode != null)
                {
                    selector.SetSiblingIndex(gameMode.GetSiblingIndex() + 1);
                    registerCreated?.Invoke(selector.gameObject);
                }
            }
            else
            {
                timingAssistButtons = selector.GetComponentsInChildren<Button>(true);
            }

            RectTransform cardRect = card as RectTransform;
            if (cardRect != null && cardRect.sizeDelta.y < 1210f)
            {
                cardRect.sizeDelta = new Vector2(cardRect.sizeDelta.x, 1210f);
            }
        }

        public void EnsureSettingsButton(Action<GameObject> registerCreated = null)
        {
            Transform card = PanelRoot == null ? null : PanelRoot.transform.Find("Setup Card");
            Transform row = card == null ? null : card.Find("Library Actions");
            if (row == null) return;
            Transform existing = row.Find("Settings");
            settingsButton = existing == null ? null : existing.GetComponent<Button>();
            if (settingsButton == null)
            {
                settingsButton = PulseForgeUIFactory.CreateButton(
                    "Settings", row, "Settings", PulseForgeUITheme.SecondaryText,
                    PulseForgeUITheme.SmallFontSize);
                PulseForgeUIFactory.SetLayoutHeight(settingsButton, 48f, 1f);
                registerCreated?.Invoke(settingsButton.gameObject);
            }
        }

        public void EnsurePersistenceControls(Action<GameObject> registerCreated = null)
        {
            Transform card = PanelRoot == null ? null : PanelRoot.transform.Find("Setup Card");
            if (card == null)
            {
                return;
            }

            Transform existingRow = card.Find("Library Actions");
            RectTransform row;
            if (existingRow == null)
            {
                row = PulseForgeUIFactory.CreateRect("Library Actions", card);
                PulseForgeUIFactory.SetLayoutHeight(row, 48f);
                HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 10f;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = true;

                saveToLibraryToggle = CreateSaveToggle(row);
                savedTracksButton = PulseForgeUIFactory.CreateButton(
                    "Saved Tracks",
                    row,
                    "Saved Tracks",
                    PulseForgeUITheme.SecondaryText,
                    PulseForgeUITheme.SmallFontSize);
                PulseForgeUIFactory.SetLayoutHeight(saveToLibraryToggle, 48f, 2f);
                PulseForgeUIFactory.SetLayoutHeight(savedTracksButton, 48f, 1f);
                if (analyzeButton != null)
                {
                    row.SetSiblingIndex(analyzeButton.transform.GetSiblingIndex());
                }

                registerCreated?.Invoke(row.gameObject);
            }
            else
            {
                row = existingRow as RectTransform;
                saveToLibraryToggle = existingRow.GetComponentInChildren<Toggle>(true);
                Transform savedButtonTransform = existingRow.Find("Saved Tracks");
                savedTracksButton = savedButtonTransform == null
                    ? null
                    : savedButtonTransform.GetComponent<Button>();
                if (saveToLibraryToggle == null)
                {
                    saveToLibraryToggle = CreateSaveToggle(existingRow);
                    PulseForgeUIFactory.SetLayoutHeight(saveToLibraryToggle, 48f, 2f);
                    registerCreated?.Invoke(saveToLibraryToggle.gameObject);
                }

                if (savedTracksButton == null)
                {
                    savedTracksButton = PulseForgeUIFactory.CreateButton(
                        "Saved Tracks",
                        existingRow,
                        "Saved Tracks",
                        PulseForgeUITheme.SecondaryText,
                        PulseForgeUITheme.SmallFontSize);
                    PulseForgeUIFactory.SetLayoutHeight(savedTracksButton, 48f, 1f);
                    registerCreated?.Invoke(savedTracksButton.gameObject);
                }
            }

            RectTransform cardRect = card as RectTransform;
            if (cardRect != null && cardRect.sizeDelta.y < 860f)
            {
                cardRect.sizeDelta = new Vector2(cardRect.sizeDelta.x, 860f);
            }
        }

        public void Bind(DebugRhythmPrototypeController controller)
        {
            if (boundController == controller)
            {
                return;
            }

            Unbind();
            boundController = controller;
            if (controller == null)
            {
                return;
            }

            PulseForgeUIFactory.BindButton(builtInDemoButton, controller.PlayBuiltInDemo);
            PulseForgeUIFactory.BindButton(chooseAudioButton, controller.SelectAudioFile);
            PulseForgeUIFactory.BindButton(analyzeButton, controller.AnalyzeSelectedAudio);
            PulseForgeUIFactory.BindButton(savedTracksButton, controller.OpenSavedTracks);
            PulseForgeUIFactory.BindButton(settingsButton, controller.OpenSettings);
            if (saveToLibraryToggle != null)
            {
                saveToLibraryToggle.onValueChanged.RemoveAllListeners();
                saveToLibraryToggle.onValueChanged.AddListener(controller.SetSaveSetupToLibrary);
            }
            detectionSelector = new ChoiceSelector<RuntimeDetectionMode>(
                detectionButtons,
                new[] { RuntimeDetectionMode.Onset, RuntimeDetectionMode.Amplitude },
                PulseForgeUITheme.Primary,
                controller.SetDetectionMode);
            difficultySelector = new ChoiceSelector<RuntimeDifficulty>(
                difficultyButtons,
                new[] { RuntimeDifficulty.Easy, RuntimeDifficulty.Normal, RuntimeDifficulty.Hard },
                PulseForgeUITheme.Primary,
                controller.SetDifficulty);
            combatStyleSelector = new ChoiceSelector<RuntimeCombatStyle>(
                combatStyleButtons,
                new[]
                {
                    RuntimeCombatStyle.Legacy,
                    RuntimeCombatStyle.Balanced,
                    RuntimeCombatStyle.Defensive,
                    RuntimeCombatStyle.Aggressive,
                    RuntimeCombatStyle.Bursty
                },
                PulseForgeUITheme.Primary,
                controller.SetCombatStyle);
            coverageSelector = new ChoiceSelector<RuntimeCoverage>(
                coverageButtons,
                new[] { RuntimeCoverage.Relaxed, RuntimeCoverage.Standard, RuntimeCoverage.FullPulse },
                PulseForgeUITheme.Primary,
                controller.SetCoverage);
            gameModeSelector = new ChoiceSelector<RadialGameMode>(
                gameModeButtons,
                new[] { RadialGameMode.Standard, RadialGameMode.Survival, RadialGameMode.OneLife },
                PulseForgeUITheme.Primary,
                controller.SetGameMode);
            timingAssistSelector = new ChoiceSelector<TimingAssistMode>(
                timingAssistButtons,
                new[] { TimingAssistMode.Standard, TimingAssistMode.Relaxed, TimingAssistMode.Practice },
                PulseForgeUITheme.Primary,
                controller.SetTimingAssist);
        }

        public void Unbind()
        {
            PulseForgeUIFactory.UnbindButton(builtInDemoButton);
            PulseForgeUIFactory.UnbindButton(chooseAudioButton);
            PulseForgeUIFactory.UnbindButton(analyzeButton);
            PulseForgeUIFactory.UnbindButton(savedTracksButton);
            PulseForgeUIFactory.UnbindButton(settingsButton);
            saveToLibraryToggle?.onValueChanged.RemoveAllListeners();
            detectionSelector?.Unbind();
            difficultySelector?.Unbind();
            combatStyleSelector?.Unbind();
            coverageSelector?.Unbind();
            gameModeSelector?.Unbind();
            timingAssistSelector?.Unbind();
            detectionSelector = null;
            difficultySelector = null;
            combatStyleSelector = null;
            coverageSelector = null;
            gameModeSelector = null;
            timingAssistSelector = null;
            boundController = null;
        }

        public void Refresh(DebugRhythmPrototypeController controller)
        {
            bool turkish = controller.ActiveUILanguage == PulseForgeUILanguage.Turkish;
            builtInDemoButton.gameObject.SetActive(controller.HasBuiltInDemo);
            selectedFileText.text = controller.HasSelectedAudio
                ? controller.SelectedAudioFileName
                : turkish ? "Özel ses seçilmedi" : "No custom audio selected";
            selectedFileText.color = controller.HasSelectedAudio
                ? PulseForgeUITheme.PrimaryText
                : PulseForgeUITheme.SecondaryText;
            analyzeButton.interactable = controller.CanAnalyzeSelectedAudio;
            if (saveToLibraryToggle != null)
            {
                saveToLibraryToggle.interactable = controller.HasSelectedAudio;
            }

            statusText.text = LocalizeSetupStatus(controller.SetupStatusMessage, turkish);
            saveToLibraryToggle?.SetIsOnWithoutNotify(controller.SaveSetupToLibrary);

            RuntimeAudioPipelineSettings settings = controller.SelectedPipelineSettings;
            detectionSelector?.SetSelected(settings.DetectionMode);
            difficultySelector?.SetSelected(settings.Difficulty);
            combatStyleSelector?.SetSelected(settings.CombatStyle);
            coverageSelector?.SetSelected(settings.Coverage);
            gameModeSelector?.SetSelected(controller.SelectedGameMode);
            timingAssistSelector?.SetSelected(controller.SelectedTimingAssist);
        }

        private static string LocalizeSetupStatus(string value, bool turkish)
        {
            if (!turkish || string.IsNullOrEmpty(value)) return value;
            switch (value)
            {
                case "Choose a song, select your settings, then analyze.":
                    return "Bir şarkı seçin, ayarları belirleyin ve analiz edin.";
                case "Song selected. Choose settings, then analyze.":
                    return "Şarkı seçildi. Ayarları belirleyin ve analiz edin.";
                default:
                    return value;
            }
        }

        public override void CollectValidationErrors(List<string> errors)
        {
            base.CollectValidationErrors(errors);
            PulseForgeUIValidation.AddMissing(errors, builtInDemoButton, "Setup: Play Built-in Demo button is missing.");
            PulseForgeUIValidation.AddMissing(errors, chooseAudioButton, "Setup: Choose Custom Audio button is missing.");
            PulseForgeUIValidation.AddMissing(errors, analyzeButton, "Setup: Analyze Song button is missing.");
            PulseForgeUIValidation.AddMissing(errors, selectedFileText, "Setup: selected audio text is missing.");
            PulseForgeUIValidation.AddMissing(errors, statusText, "Setup: status text is missing.");
            PulseForgeUIValidation.AddArray(errors, coverageButtons, 3, "Setup: coverage buttons are incomplete.");
            PulseForgeUIValidation.AddMissing(errors, saveToLibraryToggle, "Setup: Save to Library toggle is missing.");
            PulseForgeUIValidation.AddMissing(errors, savedTracksButton, "Setup: Saved Tracks button is missing.");
            PulseForgeUIValidation.AddMissing(errors, settingsButton, "Setup: Settings button is missing.");
            PulseForgeUIValidation.AddArray(errors, detectionButtons, 2, "Setup: detection buttons are incomplete.");
            PulseForgeUIValidation.AddArray(errors, difficultyButtons, 3, "Setup: difficulty buttons are incomplete.");
            PulseForgeUIValidation.AddArray(errors, combatStyleButtons, 5, "Setup: combat style buttons are incomplete.");
            PulseForgeUIValidation.AddArray(errors, gameModeButtons, 3, "Setup: game mode buttons are incomplete.");
            PulseForgeUIValidation.AddArray(errors, timingAssistButtons, 3, "Setup: timing assist buttons are incomplete.");
        }

        internal static Button[] CreateChoiceButtons(
            Transform parent,
            string label,
            IReadOnlyList<string> choices,
            int optionFontSize = PulseForgeUITheme.SmallFontSize)
        {
            RectTransform root = PulseForgeUIFactory.CreateRect(label + " Selector", parent);
            PulseForgeUIFactory.SetLayoutHeight(root, 88f);
            VerticalLayoutGroup verticalLayout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            verticalLayout.spacing = 6f;
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = true;
            verticalLayout.childForceExpandWidth = true;
            verticalLayout.childForceExpandHeight = false;

            Text labelText = PulseForgeUIFactory.CreateText(
                "Label",
                root,
                label,
                PulseForgeUITheme.SmallFontSize,
                PulseForgeUITheme.SecondaryText,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            PulseForgeUIFactory.SetLayoutHeight(labelText, 28f);
            string tooltipKey = "selector." + PulseForgeTooltipSetup.Slug(label);
            PulseForgeTooltipSetup.Attach(labelText.gameObject, tooltipKey);

            RectTransform options = PulseForgeUIFactory.CreateRect("Options", root);
            PulseForgeUIFactory.SetLayoutHeight(options, 52f);
            HorizontalLayoutGroup horizontalLayout = options.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontalLayout.spacing = 8f;
            horizontalLayout.childControlWidth = true;
            horizontalLayout.childControlHeight = true;
            horizontalLayout.childForceExpandWidth = true;
            horizontalLayout.childForceExpandHeight = true;

            Button[] buttons = new Button[choices.Count];
            for (int i = 0; i < choices.Count; i++)
            {
                buttons[i] = PulseForgeUIFactory.CreateButton(
                    choices[i],
                    options,
                    choices[i],
                    PulseForgeUITheme.Primary,
                    optionFontSize);
                PulseForgeTooltipSetup.Attach(
                    buttons[i].gameObject,
                    tooltipKey + "." + PulseForgeTooltipSetup.Slug(choices[i]));
            }

            return buttons;
        }

        private static Toggle CreateSaveToggle(Transform parent)
        {
            RectTransform root = PulseForgeUIFactory.CreateRect("Save to Library Toggle", parent);
            Toggle toggle = root.gameObject.AddComponent<Toggle>();
            toggle.isOn = false;
            toggle.transition = Selectable.Transition.ColorTint;
            toggle.colors = PulseForgeUITheme.CreateButtonColors(PulseForgeButtonStyle.Secondary);
            Navigation navigation = toggle.navigation;
            navigation.mode = Navigation.Mode.None;
            toggle.navigation = navigation;

            RectTransform box = PulseForgeUIFactory.CreateRect("Checkbox", root);
            box.anchorMin = new Vector2(0f, 0.5f);
            box.anchorMax = new Vector2(0f, 0.5f);
            box.pivot = new Vector2(0f, 0.5f);
            box.anchoredPosition = new Vector2(4f, 0f);
            box.sizeDelta = new Vector2(30f, 30f);
            Image boxImage = box.gameObject.AddComponent<Image>();
            boxImage.color = PulseForgeUITheme.SurfaceRaised;
            boxImage.sprite = PulseForgeUIFactory.RoundedSprite;
            boxImage.type = boxImage.sprite == null ? Image.Type.Simple : Image.Type.Sliced;

            RectTransform check = PulseForgeUIFactory.CreateRect("Checkmark", box);
            PulseForgeUIFactory.Stretch(check, 6f, 6f, 6f, 6f);
            Image checkImage = check.gameObject.AddComponent<Image>();
            checkImage.color = PulseForgeUITheme.Primary;
            checkImage.sprite = PulseForgeUIFactory.RoundedSprite;
            checkImage.type = checkImage.sprite == null ? Image.Type.Simple : Image.Type.Sliced;
            toggle.targetGraphic = boxImage;
            toggle.graphic = checkImage;

            Text label = PulseForgeUIFactory.CreateText(
                "Label",
                root,
                "Save this setup to Library",
                PulseForgeUITheme.SmallFontSize,
                PulseForgeUITheme.PrimaryText,
                TextAnchor.MiddleLeft);
            PulseForgeUIFactory.Stretch(label.rectTransform, 46f, 2f, 6f, 2f);
            return toggle;
        }
    }

    internal sealed class ChoiceSelector<T> where T : struct
    {
        private readonly Button[] buttons;
        private readonly T[] values;

        public ChoiceSelector(Button[] buttons, T[] values, Color accent, Action<T> selectionChanged)
        {
            this.buttons = buttons ?? Array.Empty<Button>();
            this.values = values ?? Array.Empty<T>();

            int count = Math.Min(this.buttons.Length, this.values.Length);
            for (int i = 0; i < count; i++)
            {
                T choice = this.values[i];
                PulseForgeUIFactory.BindButton(this.buttons[i], () => selectionChanged(choice));
            }
        }

        public void Unbind()
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                PulseForgeUIFactory.UnbindButton(buttons[i]);
            }
        }

        public void SetSelected(T selectedValue)
        {
            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            int count = Math.Min(buttons.Length, values.Length);
            for (int i = 0; i < count; i++)
            {
                bool isSelected = comparer.Equals(values[i], selectedValue);
                PulseForgeUIVisualStyle.ApplyButtonStyle(
                    buttons[i],
                    isSelected ? PulseForgeButtonStyle.SelectedSegment : PulseForgeButtonStyle.Segment);
            }
        }
    }

    internal static class PulseForgeUIValidation
    {
        public static void AddMissing(List<string> errors, UnityEngine.Object value, string message)
        {
            if (value == null)
            {
                errors.Add(message);
            }
        }

        public static void AddArray<T>(List<string> errors, T[] values, int expectedCount, string message)
            where T : UnityEngine.Object
        {
            if (values == null || values.Length != expectedCount)
            {
                errors.Add(message);
                return;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null)
                {
                    errors.Add(message);
                    return;
                }
            }
        }
    }
}
