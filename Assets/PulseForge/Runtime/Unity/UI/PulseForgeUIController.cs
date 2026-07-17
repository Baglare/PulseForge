using PulseForge.Runtime.Unity.Prototype;
using PulseForge.Runtime.Unity.Persistence;
using UnityEngine;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class PulseForgeUIController : MonoBehaviour
    {
        private DebugRhythmPrototypeController runtimeController;
        private PulseForgeSceneUIRoot sceneRoot;
        private bool isBound;
        private PulseForgeUIState? lastTooltipState;
        private bool lastSettingsVisibility;
        private PulseForgeUILanguage lastLanguage = (PulseForgeUILanguage)(-1);
        private int lastLocalizationRevision = -1;

        public PulseForgeSceneUIRoot SceneRoot => sceneRoot;
        public bool IsBound => isBound;

        public void Bind(DebugRhythmPrototypeController controller, PulseForgeSceneUIRoot root)
        {
            if (isBound && runtimeController == controller && sceneRoot == root)
            {
                Refresh();
                return;
            }

            Unbind();
            runtimeController = controller;
            sceneRoot = root;
            if (runtimeController == null || sceneRoot == null)
            {
                return;
            }

            sceneRoot.SetupPanel?.Bind(runtimeController);
            sceneRoot.SavedTracksPanel?.Bind(runtimeController);
            sceneRoot.SettingsPanel?.Bind(runtimeController);
            sceneRoot.ReadyPanel?.Bind(runtimeController);
            sceneRoot.GameplayHud?.Bind(runtimeController);
            sceneRoot.PauseOverlay?.Bind(runtimeController);
            sceneRoot.ResultsPanel?.Bind(runtimeController);
            sceneRoot.ErrorPanel?.Bind(runtimeController);
            sceneRoot.GameplayFeedbackController?.Bind(runtimeController);
            sceneRoot.RadialPresentationController?.Bind(runtimeController);
            sceneRoot.TooltipView?.Bind(runtimeController);
            isBound = true;
            Refresh();
        }

        public void Unbind()
        {
            if (sceneRoot != null)
            {
                sceneRoot.CompleteMotionTransitions();
                sceneRoot.GameplayFeedbackController?.Unbind();
                sceneRoot.RadialPresentationController?.Unbind();
                sceneRoot.TooltipView?.Unbind();
                sceneRoot.SetupPanel?.Unbind();
                sceneRoot.SavedTracksPanel?.Unbind();
                sceneRoot.SettingsPanel?.Unbind();
                sceneRoot.ReadyPanel?.Unbind();
                sceneRoot.GameplayHud?.Unbind();
                sceneRoot.PauseOverlay?.Unbind();
                sceneRoot.ResultsPanel?.Unbind();
                sceneRoot.ErrorPanel?.Unbind();
                sceneRoot.GameplayHud?.SetRhythmLaneVisible(true);
            }

            runtimeController = null;
            sceneRoot = null;
            isBound = false;
            lastTooltipState = null;
            lastSettingsVisibility = false;
            lastLanguage = (PulseForgeUILanguage)(-1);
            lastLocalizationRevision = -1;
        }

        public void Refresh()
        {
            if (!isBound || runtimeController == null || sceneRoot == null)
            {
                return;
            }

            PulseForgeUIState state = runtimeController.UIState;
            bool settingsVisible = runtimeController.IsSettingsOpen;
            if (lastTooltipState != state || lastSettingsVisibility != settingsVisible)
            {
                sceneRoot.TooltipView?.HideAll();
                lastTooltipState = state;
                lastSettingsVisibility = settingsVisible;
            }
            sceneRoot.ApplyVisibility(state);
            bool showSavedTracks = state == PulseForgeUIState.Setup
                && runtimeController.IsSavedTracksOpen;
            sceneRoot.ApplyAuxiliaryVisibility(state, showSavedTracks);
            sceneRoot.ApplySettingsVisibility(runtimeController.IsSettingsOpen);
            sceneRoot.GameplayHud?.SetRhythmLaneVisible(
                !runtimeController.UsesRadialCombatPresentation);
            sceneRoot.RadialPresentationController?.Refresh(runtimeController);
            if (runtimeController.IsSettingsOpen)
            {
                sceneRoot.SettingsPanel?.Refresh(runtimeController);
            }
            switch (state)
            {
                case PulseForgeUIState.Setup:
                    if (showSavedTracks)
                    {
                        sceneRoot.SavedTracksPanel?.Refresh(runtimeController);
                    }
                    else
                    {
                        sceneRoot.SetupPanel?.Refresh(runtimeController);
                    }
                    break;
                case PulseForgeUIState.Processing:
                    sceneRoot.ProcessingPanel?.Refresh(runtimeController);
                    break;
                case PulseForgeUIState.Ready:
                    sceneRoot.ReadyPanel?.Refresh(runtimeController);
                    break;
                case PulseForgeUIState.Countdown:
                    sceneRoot.GameplayHud?.Refresh(runtimeController);
                    sceneRoot.CountdownOverlay?.Refresh(runtimeController);
                    break;
                case PulseForgeUIState.Playing:
                case PulseForgeUIState.Paused:
                    sceneRoot.GameplayHud?.Refresh(runtimeController);
                    break;
                case PulseForgeUIState.Completed:
                case PulseForgeUIState.Failed:
                    sceneRoot.ResultsPanel?.Refresh(runtimeController);
                    break;
                case PulseForgeUIState.Error:
                    sceneRoot.ErrorPanel?.Refresh(runtimeController);
                    break;
            }

            sceneRoot.RefreshMotion(state);
            PulseForgeUILanguage language = runtimeController.ActiveUILanguage;
            int localizationRevision = runtimeController.IsSettingsOpen
                ? runtimeController.SettingsDraftRevision
                : -1;
            if (lastLanguage != language || lastLocalizationRevision != localizationRevision)
            {
                PulseForgeUILocalization.Apply(sceneRoot, language);
                lastLanguage = language;
                lastLocalizationRevision = localizationRevision;
            }
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
