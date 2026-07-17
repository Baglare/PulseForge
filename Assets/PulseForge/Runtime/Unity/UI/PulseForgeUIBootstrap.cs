using System.Collections.Generic;
using PulseForge.Runtime.Unity.Prototype;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class PulseForgeUIBootstrap : MonoBehaviour
    {
        private const string RuntimeFallbackRootName = "PulseForge Runtime UI";

        [SerializeField] private PulseForgeSceneUIRoot sceneUIRoot;

        public PulseForgeSceneUIRoot SceneUIRoot => sceneUIRoot;
        public bool UsedRuntimeFallback { get; private set; }

        public static PulseForgeUIController EnsureFor(DebugRhythmPrototypeController controller)
        {
            PulseForgeUIBootstrap bootstrap = controller.GetComponent<PulseForgeUIBootstrap>();
            if (bootstrap == null)
            {
                bootstrap = controller.gameObject.AddComponent<PulseForgeUIBootstrap>();
            }

            return bootstrap.Ensure(controller);
        }

        public void AssignSceneUIRoot(PulseForgeSceneUIRoot value)
        {
            sceneUIRoot = value;
        }

        public PulseForgeUIController Ensure(DebugRhythmPrototypeController controller)
        {
            if (controller == null)
            {
                return null;
            }

            UsedRuntimeFallback = false;
            PulseForgeSceneUIRoot root = ResolveSceneRoot();
            if (root == null)
            {
                root = PulseForgeUIFactory.CreateStaticHierarchy(null, RuntimeFallbackRootName);
                sceneUIRoot = root;
                UsedRuntimeFallback = true;
                Debug.LogWarning(
                    "PulseForge scene-authored UI was not found. Runtime UI fallback was created. "
                    + "Use Tools > PulseForge > UI > Materialize Runtime UI Into Scene for the normal prototype scene.",
                    controller);
            }

            PulseForgePersistenceUISetup.Apply(root);
            PulseForgeSettingsUISetup.Apply(root);
            PulseForgeM9HUISetup.Apply(root);
            root.SetupPanel?.EnsureViewportLayout();
            PulseForgeUIMotionSetup.Apply(root);
            PulseForgeGameplayFeedbackSetup.Apply(root);
            RadialSaboteurFogSetup.Apply(root);
            PulseForgeGameModesUISetup.Apply(root);
            PulseForgeCoverageUISetup.Apply(root);
            PulseForgePlayabilityAssistUISetup.Apply(root);
            RadialForecastSetup.Apply(root);
            RadialGroupTimingSetup.Apply(root);
            RadialArenaVisualSetup.Apply(root);
            RadialCombatVfxSetup.Apply(root);
            root.ProcessingPanel?.EnsureV2Stages();
            root.ReadyPanel?.EnsureV2SummaryFields();
            PulseForgeTooltipSetup.Apply(root);
            root.SetEnableMotion(controller.MotionEnabledSetting);
            EventSystem eventSystem = EnsureEventSystem(root, UsedRuntimeFallback);
            root.AssignEventSystem(eventSystem);

            List<string> validationErrors = new List<string>();
            root.CollectValidationErrors(validationErrors);
            if (validationErrors.Count > 0)
            {
                Debug.LogError(
                    "PulseForge scene UI has missing references:\n- " + string.Join("\n- ", validationErrors),
                    root);
            }

            PulseForgeUIController uiController = root.GetComponent<PulseForgeUIController>();
            if (uiController == null)
            {
                uiController = root.gameObject.AddComponent<PulseForgeUIController>();
            }

            uiController.Bind(controller, root);
            return uiController;
        }

        private PulseForgeSceneUIRoot ResolveSceneRoot()
        {
            if (sceneUIRoot != null)
            {
                return sceneUIRoot;
            }

            PulseForgeSceneUIRoot[] roots = Object.FindObjectsByType<PulseForgeSceneUIRoot>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (roots.Length == 1)
            {
                sceneUIRoot = roots[0];
                return sceneUIRoot;
            }

            if (roots.Length > 1)
            {
                Debug.LogError(
                    "Multiple PulseForgeSceneUIRoot components were found. Assign the intended root on PulseForgeUIBootstrap.",
                    this);
                sceneUIRoot = roots[0];
                return sceneUIRoot;
            }

            return null;
        }

        private static EventSystem EnsureEventSystem(PulseForgeSceneUIRoot root, bool parentFallbackToRoot)
        {
            EventSystem eventSystem = root.EventSystem;
            if (eventSystem == null)
            {
                EventSystem[] eventSystems = Object.FindObjectsByType<EventSystem>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                if (eventSystems.Length > 0)
                {
                    eventSystem = eventSystems[0];
                }
            }

            if (eventSystem == null)
            {
                eventSystem = PulseForgeUIFactory.CreateEventSystem(parentFallbackToRoot ? root.transform : null);
            }

            InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            if (inputModule.actionsAsset == null)
            {
                inputModule.AssignDefaultActions();
            }

            return eventSystem;
        }
    }

}
