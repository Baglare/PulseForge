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
        [SerializeField] private ProcessingPanelView processingPanel;
        [SerializeField] private ReadyPanelView readyPanel;
        [SerializeField] private GameplayHUDView gameplayHud;
        [SerializeField] private CountdownOverlayView countdownOverlay;
        [SerializeField] private PauseMenuView pauseOverlay;
        [SerializeField] private ResultsPanelView resultsPanel;
        [SerializeField] private ErrorPanelView errorPanel;
        [SerializeField] private EventSystem eventSystem;

        public Canvas Canvas => canvas;
        public GameObject Background => background;
        public SetupPanelView SetupPanel => setupPanel;
        public ProcessingPanelView ProcessingPanel => processingPanel;
        public ReadyPanelView ReadyPanel => readyPanel;
        public GameplayHUDView GameplayHud => gameplayHud;
        public CountdownOverlayView CountdownOverlay => countdownOverlay;
        public PauseMenuView PauseOverlay => pauseOverlay;
        public ResultsPanelView ResultsPanel => resultsPanel;
        public ErrorPanelView ErrorPanel => errorPanel;
        public EventSystem EventSystem => eventSystem;

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

        public void ApplyVisibility(PulseForgeUIState state)
        {
            bool showGameplay = state == PulseForgeUIState.Countdown
                || state == PulseForgeUIState.Playing
                || state == PulseForgeUIState.Paused;

            SetActive(setupPanel, state == PulseForgeUIState.Setup);
            SetActive(processingPanel, state == PulseForgeUIState.Processing);
            SetActive(readyPanel, state == PulseForgeUIState.Ready);
            SetActive(gameplayHud, showGameplay);
            SetActive(countdownOverlay, state == PulseForgeUIState.Countdown);
            SetActive(pauseOverlay, state == PulseForgeUIState.Paused);
            SetActive(resultsPanel, state == PulseForgeUIState.Completed);
            SetActive(errorPanel, state == PulseForgeUIState.Error);
        }

        public void ShowAllPanels()
        {
            SetActive(setupPanel, true);
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
            AddMissing(errors, processingPanel, "Processing panel reference is missing.");
            AddMissing(errors, readyPanel, "Ready panel reference is missing.");
            AddMissing(errors, gameplayHud, "Gameplay HUD reference is missing.");
            AddMissing(errors, countdownOverlay, "Countdown overlay reference is missing.");
            AddMissing(errors, pauseOverlay, "Pause overlay reference is missing.");
            AddMissing(errors, resultsPanel, "Results panel reference is missing.");
            AddMissing(errors, errorPanel, "Error panel reference is missing.");
            AddMissing(errors, eventSystem, "EventSystem reference is missing.");

            setupPanel?.CollectValidationErrors(errors);
            processingPanel?.CollectValidationErrors(errors);
            readyPanel?.CollectValidationErrors(errors);
            gameplayHud?.CollectValidationErrors(errors);
            countdownOverlay?.CollectValidationErrors(errors);
            pauseOverlay?.CollectValidationErrors(errors);
            resultsPanel?.CollectValidationErrors(errors);
            errorPanel?.CollectValidationErrors(errors);
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
