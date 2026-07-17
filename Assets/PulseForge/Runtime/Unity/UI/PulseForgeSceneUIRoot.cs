using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class PulseForgeSceneUIRoot : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private GameObject background;
        [SerializeField] private SetupPanelView setupPanel;
        [SerializeField] private SavedTracksPanelView savedTracksPanel;
        [SerializeField] private SettingsPanelView settingsPanel;
        [SerializeField] private ProcessingPanelView processingPanel;
        [SerializeField] private ReadyPanelView readyPanel;
        [SerializeField] private GameplayHUDView gameplayHud;
        [SerializeField] private CountdownOverlayView countdownOverlay;
        [SerializeField] private PauseMenuView pauseOverlay;
        [SerializeField] private ResultsPanelView resultsPanel;
        [SerializeField] private ErrorPanelView errorPanel;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private bool enableMotion = true;
        [SerializeField] private PulseForgeUIMotionController motionController;
        [SerializeField] private PulseForgeGameplayFeedbackController gameplayFeedbackController;
        [SerializeField] private RadialCombatStageView radialCombatStage;
        [SerializeField] private RadialCombatPresentationController radialPresentationController;
        [SerializeField] private PulseForgeTooltipView tooltipView;

        public Canvas Canvas => canvas;
        public GameObject Background => background;
        public SetupPanelView SetupPanel => setupPanel;
        public SavedTracksPanelView SavedTracksPanel => savedTracksPanel;
        public SettingsPanelView SettingsPanel => settingsPanel;
        public ProcessingPanelView ProcessingPanel => processingPanel;
        public ReadyPanelView ReadyPanel => readyPanel;
        public GameplayHUDView GameplayHud => gameplayHud;
        public CountdownOverlayView CountdownOverlay => countdownOverlay;
        public PauseMenuView PauseOverlay => pauseOverlay;
        public ResultsPanelView ResultsPanel => resultsPanel;
        public ErrorPanelView ErrorPanel => errorPanel;
        public EventSystem EventSystem => eventSystem;
        public bool EnableMotion => enableMotion;
        public PulseForgeUIMotionController MotionController => motionController;
        public PulseForgeGameplayFeedbackController GameplayFeedbackController => gameplayFeedbackController;
        public RadialCombatStageView RadialCombatStage => radialCombatStage;
        public RadialCombatPresentationController RadialPresentationController => radialPresentationController;
        public PulseForgeTooltipView TooltipView => tooltipView;

        public void Configure(
            Canvas sceneCanvas,
            GameObject sceneBackground,
            SetupPanelView setup,
            ProcessingPanelView processing,
            ReadyPanelView ready,
            GameplayHUDView gameplay,
            CountdownOverlayView countdown,
            PauseMenuView pause,
            ResultsPanelView results,
            ErrorPanelView error)
        {
            canvas = sceneCanvas;
            background = sceneBackground;
            setupPanel = setup;
            processingPanel = processing;
            readyPanel = ready;
            gameplayHud = gameplay;
            countdownOverlay = countdown;
            pauseOverlay = pause;
            resultsPanel = results;
            errorPanel = error;
        }

        public void AssignEventSystem(EventSystem value)
        {
            eventSystem = value;
        }

        public void ConfigureMotion(PulseForgeUIMotionController value)
        {
            motionController = value;
        }

        public void ConfigureGameplayFeedback(PulseForgeGameplayFeedbackController value)
        {
            gameplayFeedbackController = value;
        }

        public void ConfigureRadialCombatStage(
            RadialCombatStageView stage,
            RadialCombatPresentationController presentationController)
        {
            radialCombatStage = stage;
            radialPresentationController = presentationController;
        }

        public void ConfigureTooltip(PulseForgeTooltipView value)
        {
            tooltipView = value;
        }

        public void ConfigureSavedTracksPanel(SavedTracksPanelView value)
        {
            savedTracksPanel = value;
        }

        public void ConfigureSettingsPanel(SettingsPanelView value)
        {
            settingsPanel = value;
        }

        public void ApplySettingsVisibility(bool visible)
        {
            SetActive(settingsPanel, visible);
        }

        public void ApplyAuxiliaryVisibility(PulseForgeUIState state, bool showSavedTracks)
        {
            bool showLibrary = state == PulseForgeUIState.Setup && showSavedTracks;
            SetActive(savedTracksPanel, showLibrary);
            if (state == PulseForgeUIState.Setup)
            {
                SetActive(setupPanel, !showLibrary);
            }
        }

        public void SetEnableMotion(bool value)
        {
            enableMotion = value;
            if (!enableMotion)
            {
                CompleteMotionTransitions();
            }
        }

        public void ApplyVisibility(PulseForgeUIState state)
        {
            radialCombatStage?.SetUIStateVisibility(state);
            if (Application.isPlaying && motionController != null)
            {
                motionController.ShowState(state, enableMotion);
                return;
            }

            ApplyVisibilityImmediate(state);
        }

        public void RefreshMotion(PulseForgeUIState state)
        {
            if (Application.isPlaying && motionController != null)
            {
                motionController.RefreshDynamicMotion(state, enableMotion);
            }
        }

        public void CompleteMotionTransitions()
        {
            motionController?.CompleteCurrentState();
        }

        private void ApplyVisibilityImmediate(PulseForgeUIState state)
        {
            bool showGameplay = state == PulseForgeUIState.Countdown
                || state == PulseForgeUIState.Playing
                || state == PulseForgeUIState.Paused;

            SetActive(setupPanel, state == PulseForgeUIState.Setup);
            SetActive(savedTracksPanel, false);
            SetActive(settingsPanel, false);
            SetActive(processingPanel, state == PulseForgeUIState.Processing);
            SetActive(readyPanel, state == PulseForgeUIState.Ready);
            SetActive(gameplayHud, showGameplay);
            SetActive(countdownOverlay, state == PulseForgeUIState.Countdown);
            SetActive(pauseOverlay, state == PulseForgeUIState.Paused);
            SetActive(
                resultsPanel,
                state == PulseForgeUIState.Completed || state == PulseForgeUIState.Failed);
            SetActive(errorPanel, state == PulseForgeUIState.Error);
            radialCombatStage?.SetUIStateVisibility(state);
        }

        public void ShowAllPanels()
        {
            SetActive(setupPanel, true);
            SetActive(savedTracksPanel, true);
            SetActive(settingsPanel, true);
            SetActive(processingPanel, true);
            SetActive(readyPanel, true);
            SetActive(gameplayHud, true);
            SetActive(countdownOverlay, true);
            SetActive(pauseOverlay, true);
            SetActive(resultsPanel, true);
            SetActive(errorPanel, true);
        }

        public bool CollectValidationErrors(List<string> errors)
        {
            if (errors == null)
            {
                return false;
            }

            AddMissing(errors, canvas, "Canvas reference is missing.");
            AddMissing(errors, background, "Background reference is missing.");
            AddMissing(errors, setupPanel, "Setup panel reference is missing.");
            AddMissing(errors, savedTracksPanel, "M8C Saved Tracks panel reference is missing.");
            AddMissing(errors, settingsPanel, "M8D Settings panel reference is missing.");
            AddMissing(errors, processingPanel, "Processing panel reference is missing.");
            AddMissing(errors, readyPanel, "Ready panel reference is missing.");
            AddMissing(errors, gameplayHud, "Gameplay HUD reference is missing.");
            AddMissing(errors, countdownOverlay, "Countdown overlay reference is missing.");
            AddMissing(errors, pauseOverlay, "Pause overlay reference is missing.");
            AddMissing(errors, resultsPanel, "Results panel reference is missing.");
            AddMissing(errors, errorPanel, "Error panel reference is missing.");
            AddMissing(errors, eventSystem, "EventSystem reference is missing.");
            AddMissing(errors, motionController, "M8B.1 motion controller is missing.");
            AddMissing(errors, gameplayFeedbackController, "M8B.2 gameplay feedback controller is missing.");
            AddMissing(errors, radialCombatStage, "M9D.1 Radial Combat Stage is missing.");
            AddMissing(errors, radialPresentationController, "M9D.1 radial presentation controller is missing.");

            setupPanel?.CollectValidationErrors(errors);
            savedTracksPanel?.CollectValidationErrors(errors);
            settingsPanel?.CollectValidationErrors(errors);
            processingPanel?.CollectValidationErrors(errors);
            readyPanel?.CollectValidationErrors(errors);
            gameplayHud?.CollectValidationErrors(errors);
            countdownOverlay?.CollectValidationErrors(errors);
            pauseOverlay?.CollectValidationErrors(errors);
            resultsPanel?.CollectValidationErrors(errors);
            errorPanel?.CollectValidationErrors(errors);
            motionController?.CollectValidationErrors(errors);
            gameplayFeedbackController?.CollectValidationErrors(errors);
            radialCombatStage?.CollectValidationErrors(errors);
            return errors.Count == 0;
        }

        private static void SetActive(PulseForgePanelView panel, bool isActive)
        {
            if (panel != null)
            {
                panel.SetActive(isActive);
            }
        }

        private static void AddMissing(List<string> errors, Object value, string message)
        {
            if (value == null)
            {
                errors.Add(message);
            }
        }
    }

    public abstract class PulseForgePanelView : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;

        public GameObject PanelRoot => panelRoot == null ? gameObject : panelRoot;

        public void ConfigurePanelRoot(GameObject value)
        {
            panelRoot = value;
        }

        public void SetActive(bool isActive)
        {
            GameObject target = PanelRoot;
            if (target.activeSelf != isActive)
            {
                target.SetActive(isActive);
            }
        }

        public virtual void CollectValidationErrors(List<string> errors)
        {
            if (panelRoot == null)
            {
                errors.Add(GetType().Name + ": panel root is missing.");
            }
        }
    }
}
