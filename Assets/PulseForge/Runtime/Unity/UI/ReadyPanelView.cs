using System.Collections.Generic;
using PulseForge.Runtime.Unity.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class ReadyPanelView : PulseForgePanelView
    {
        [SerializeField] private Text songText;
        [SerializeField] private Text eventCountText;
        [SerializeField] private Text detectionText;
        [SerializeField] private Text difficultyText;
        [SerializeField] private Text combatStyleText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button anotherSongButton;

        private DebugRhythmPrototypeController boundController;

        public static ReadyPanelView Create(Transform parent)
        {
            RectTransform card = FlowPanelBuilder.CreateScreenWithCard(
                "Ready Panel",
                parent,
                new Vector2(820f, 780f),
                out GameObject root);
            ReadyPanelView view = root.AddComponent<ReadyPanelView>();
            view.ConfigurePanelRoot(root);

            FlowPanelBuilder.AddHeading(card, "Track Ready");
            view.songText = FlowPanelBuilder.AddValue(card, "Song");
            view.eventCountText = FlowPanelBuilder.AddValue(card, "Events");
            view.detectionText = FlowPanelBuilder.AddValue(card, "Detection");
            view.difficultyText = FlowPanelBuilder.AddValue(card, "Difficulty");
            view.combatStyleText = FlowPanelBuilder.AddValue(card, "Combat Style");
            view.startButton = FlowPanelBuilder.AddButton(card, "Start", PulseForgeUITheme.Primary, 76f, 30);
            view.settingsButton = FlowPanelBuilder.AddButton(card, "Change Settings", PulseForgeUITheme.SecondaryText);
            view.anotherSongButton = FlowPanelBuilder.AddButton(card, "Choose Another Song", PulseForgeUITheme.SurfaceSoft);
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

            PulseForgeUIFactory.BindButton(startButton, controller.StartSession);
            PulseForgeUIFactory.BindButton(settingsButton, controller.ChangeSettings);
            PulseForgeUIFactory.BindButton(anotherSongButton, controller.ChooseAnotherSong);
        }

        public void Unbind()
        {
            PulseForgeUIFactory.UnbindButton(startButton);
            PulseForgeUIFactory.UnbindButton(settingsButton);
            PulseForgeUIFactory.UnbindButton(anotherSongButton);
            boundController = null;
        }

        public void Refresh(DebugRhythmPrototypeController controller)
        {
            songText.text = controller.SongName;
            eventCountText.text = "EVENTS     " + controller.SessionEventCount;
            detectionText.text = "DETECTION     " + controller.AppliedDetectionLabel;
            difficultyText.text = "DIFFICULTY     " + controller.AppliedDifficultyLabel;
            combatStyleText.text = "COMBAT STYLE     " + controller.AppliedCombatStyleLabel;
            startButton.interactable = controller.CanStart;
        }

        public override void CollectValidationErrors(List<string> errors)
        {
            base.CollectValidationErrors(errors);
            PulseForgeUIValidation.AddMissing(errors, songText, "Ready: song text is missing.");
            PulseForgeUIValidation.AddMissing(errors, eventCountText, "Ready: event count text is missing.");
            PulseForgeUIValidation.AddMissing(errors, detectionText, "Ready: detection text is missing.");
            PulseForgeUIValidation.AddMissing(errors, difficultyText, "Ready: difficulty text is missing.");
            PulseForgeUIValidation.AddMissing(errors, combatStyleText, "Ready: combat style text is missing.");
            PulseForgeUIValidation.AddMissing(errors, startButton, "Ready: Start button is missing.");
            PulseForgeUIValidation.AddMissing(errors, settingsButton, "Ready: Change Settings button is missing.");
            PulseForgeUIValidation.AddMissing(errors, anotherSongButton, "Ready: Choose Another Song button is missing.");
        }
    }
}
