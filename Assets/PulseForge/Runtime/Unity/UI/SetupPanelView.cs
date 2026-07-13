using System;
using System.Collections.Generic;
using PulseForge.Runtime.Unity.Audio;
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
        [SerializeField] private Button[] detectionButtons;
        [SerializeField] private Button[] difficultyButtons;
        [SerializeField] private Button[] combatStyleButtons;

        private DebugRhythmPrototypeController boundController;
        private ChoiceSelector<RuntimeDetectionMode> detectionSelector;
        private ChoiceSelector<RuntimeDifficulty> difficultySelector;
        private ChoiceSelector<RuntimeCombatStyle> combatStyleSelector;

        public Button BuiltInDemoButton => builtInDemoButton;
        public Button ChooseAudioButton => chooseAudioButton;
        public Button AnalyzeButton => analyzeButton;
        public Text SelectedFileText => selectedFileText;

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
            return view;
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
        }

        public void Unbind()
        {
            PulseForgeUIFactory.UnbindButton(builtInDemoButton);
            PulseForgeUIFactory.UnbindButton(chooseAudioButton);
            PulseForgeUIFactory.UnbindButton(analyzeButton);
            detectionSelector?.Unbind();
            difficultySelector?.Unbind();
            combatStyleSelector?.Unbind();
            detectionSelector = null;
            difficultySelector = null;
            combatStyleSelector = null;
            boundController = null;
        }

        public void Refresh(DebugRhythmPrototypeController controller)
        {
            builtInDemoButton.gameObject.SetActive(controller.HasBuiltInDemo);
            selectedFileText.text = controller.HasSelectedAudio
                ? controller.SelectedAudioFileName
                : "No custom audio selected";
            selectedFileText.color = controller.HasSelectedAudio
                ? PulseForgeUITheme.PrimaryText
                : PulseForgeUITheme.SecondaryText;
            analyzeButton.interactable = controller.CanAnalyzeSelectedAudio;
            statusText.text = controller.SetupStatusMessage;

            RuntimeAudioPipelineSettings settings = controller.SelectedPipelineSettings;
            detectionSelector?.SetSelected(settings.DetectionMode);
            difficultySelector?.SetSelected(settings.Difficulty);
            combatStyleSelector?.SetSelected(settings.CombatStyle);
        }

        public override void CollectValidationErrors(List<string> errors)
        {
            base.CollectValidationErrors(errors);
            PulseForgeUIValidation.AddMissing(errors, builtInDemoButton, "Setup: Play Built-in Demo button is missing.");
            PulseForgeUIValidation.AddMissing(errors, chooseAudioButton, "Setup: Choose Custom Audio button is missing.");
            PulseForgeUIValidation.AddMissing(errors, analyzeButton, "Setup: Analyze Song button is missing.");
            PulseForgeUIValidation.AddMissing(errors, selectedFileText, "Setup: selected audio text is missing.");
            PulseForgeUIValidation.AddMissing(errors, statusText, "Setup: status text is missing.");
            PulseForgeUIValidation.AddArray(errors, detectionButtons, 2, "Setup: detection buttons are incomplete.");
            PulseForgeUIValidation.AddArray(errors, difficultyButtons, 3, "Setup: difficulty buttons are incomplete.");
            PulseForgeUIValidation.AddArray(errors, combatStyleButtons, 5, "Setup: combat style buttons are incomplete.");
        }

        private static Button[] CreateChoiceButtons(
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
            }

            return buttons;
        }
    }

    internal sealed class ChoiceSelector<T> where T : struct
    {
        private readonly Button[] buttons;
        private readonly T[] values;
        private readonly Color accent;

        public ChoiceSelector(Button[] buttons, T[] values, Color accent, Action<T> selectionChanged)
        {
            this.buttons = buttons ?? Array.Empty<Button>();
            this.values = values ?? Array.Empty<T>();
            this.accent = accent;

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
                ColorBlock colors = PulseForgeUITheme.CreateButtonColors(accent, isSelected);
                buttons[i].colors = colors;
                Image image = buttons[i].targetGraphic as Image;
                if (image != null)
                {
                    image.color = colors.normalColor;
                }
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
