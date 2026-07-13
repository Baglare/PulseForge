using System.Collections.Generic;
using PulseForge.Runtime.Unity.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class PauseMenuView : PulseForgePanelView
    {
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button changeSongButton;
        [SerializeField] private Button settingsButton;

        private DebugRhythmPrototypeController boundController;

        public static PauseMenuView Create(Transform parent)
        {
            RectTransform overlay = PulseForgeUIFactory.CreatePanel("Pause Overlay", parent, PulseForgeUITheme.Overlay);
            PauseMenuView view = overlay.gameObject.AddComponent<PauseMenuView>();
            view.ConfigurePanelRoot(overlay.gameObject);
            RectTransform card = PulseForgeUIFactory.CreateCenteredCard(
                "Pause Card",
                overlay,
                new Vector2(640f, 570f),
                PulseForgeUITheme.SurfaceRaised);
            VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(54, 54, 48, 48);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Text title = PulseForgeUIFactory.CreateText(
                "Title", card, "PAUSED", 52, PulseForgeUITheme.PrimaryText,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            PulseForgeUIFactory.SetLayoutHeight(title, 96f);
            view.resumeButton = FlowPanelBuilder.AddButton(card, "Resume", PulseForgeUITheme.Primary, 72f, 28);
            view.restartButton = FlowPanelBuilder.AddButton(card, "Restart Track", PulseForgeUITheme.SecondaryText);
            view.settingsButton = FlowPanelBuilder.AddButton(card, "Settings", PulseForgeUITheme.SecondaryText);
            view.changeSongButton = FlowPanelBuilder.AddButton(card, "Change Song", PulseForgeUITheme.SecondaryText);
            return view;
        }

        public void EnsureSettingsButton(System.Action<GameObject> registerCreated = null)
        {
            Transform card = PanelRoot == null ? null : PanelRoot.transform.Find("Pause Card");
            if (card == null) return;
            Transform existing = card.Find("Settings");
            settingsButton = existing == null ? null : existing.GetComponent<Button>();
            if (settingsButton == null)
            {
                settingsButton = FlowPanelBuilder.AddButton((RectTransform)card, "Settings", PulseForgeUITheme.SecondaryText);
                if (changeSongButton != null) settingsButton.transform.SetSiblingIndex(changeSongButton.transform.GetSiblingIndex());
                registerCreated?.Invoke(settingsButton.gameObject);
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

            PulseForgeUIFactory.BindButton(resumeButton, controller.ResumeSession);
            PulseForgeUIFactory.BindButton(restartButton, controller.RestartSession);
            PulseForgeUIFactory.BindButton(settingsButton, controller.OpenSettings);
            PulseForgeUIFactory.BindButton(changeSongButton, controller.ChooseAnotherSong);
        }

        public void Unbind()
        {
            PulseForgeUIFactory.UnbindButton(resumeButton);
            PulseForgeUIFactory.UnbindButton(restartButton);
            PulseForgeUIFactory.UnbindButton(settingsButton);
            PulseForgeUIFactory.UnbindButton(changeSongButton);
            boundController = null;
        }

        public override void CollectValidationErrors(List<string> errors)
        {
            base.CollectValidationErrors(errors);
            PulseForgeUIValidation.AddMissing(errors, resumeButton, "Pause: Resume button is missing.");
            PulseForgeUIValidation.AddMissing(errors, restartButton, "Pause: Restart Track button is missing.");
            PulseForgeUIValidation.AddMissing(errors, settingsButton, "Pause: Settings button is missing.");
            PulseForgeUIValidation.AddMissing(errors, changeSongButton, "Pause: Change Song button is missing.");
        }
    }
}
