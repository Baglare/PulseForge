using PulseForge.Runtime.Unity.Prototype;
using UnityEngine;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class PulseForgeUIController : MonoBehaviour
    {
        private DebugRhythmPrototypeController runtimeController;
        private PulseForgeSceneUIRoot sceneRoot;
        private bool isBound;

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
        }

        public void Refresh()
        {
            if (!isBound || runtimeController == null || sceneRoot == null)
            {
                return;
            }

            PulseForgeUIState state = runtimeController.UIState;
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
        }

        private void Update()
        {
            Refresh();
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
