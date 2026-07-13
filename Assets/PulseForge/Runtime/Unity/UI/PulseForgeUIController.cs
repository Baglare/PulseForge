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
            sceneRoot.ReadyPanel?.Bind(runtimeController);
            sceneRoot.GameplayHud?.Bind(runtimeController);
            sceneRoot.PauseOverlay?.Bind(runtimeController);
            sceneRoot.ResultsPanel?.Bind(runtimeController);
            sceneRoot.ErrorPanel?.Bind(runtimeController);
            sceneRoot.GameplayFeedbackController?.Bind(runtimeController);
            isBound = true;
            Refresh();
        }

        public void Unbind()
        {
            if (sceneRoot != null)
            {
                sceneRoot.CompleteMotionTransitions();
                sceneRoot.GameplayFeedbackController?.Unbind();
                sceneRoot.SetupPanel?.Unbind();
                sceneRoot.ReadyPanel?.Unbind();
                sceneRoot.GameplayHud?.Unbind();
                sceneRoot.PauseOverlay?.Unbind();
                sceneRoot.ResultsPanel?.Unbind();
                sceneRoot.ErrorPanel?.Unbind();
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
            switch (state)
            {
                case PulseForgeUIState.Setup:
                    sceneRoot.SetupPanel?.Refresh(runtimeController);
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
