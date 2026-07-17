using System.Collections.Generic;
using System.Linq;
using PulseForge.Runtime.Unity.Prototype;
using PulseForge.Runtime.Unity.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PulseForge.Editor.UI
{
    internal static class PulseForgeSceneUIMaterializer
    {
        private const string MaterializeMenu = "Tools/PulseForge/UI/Materialize Runtime UI Into Scene";
        private const string ApplyM8AStyleMenu = "Tools/PulseForge/UI/Apply M8A Visual Style";
        private const string ApplyM8B1MotionMenu = "Tools/PulseForge/UI/Apply M8B.1 Motion Setup";
        private const string ApplyM8B2FeedbackMenu = "Tools/PulseForge/UI/Apply M8B.2 Gameplay Feedback Setup";
        private const string ApplyM8CPersistenceMenu = "Tools/PulseForge/UI/Apply M8C Persistence UI Setup";
        private const string ApplyM8DSettingsMenu = "Tools/PulseForge/UI/Apply M8D Settings UI Setup";
        private const string ApplyM9D1RadialStageMenu = "Tools/PulseForge/UI/Apply M9D.1 Radial Stage Setup";
        private const string ApplyM9D2CompoundVisualMenu = "Tools/PulseForge/UI/Apply M9D.2 Compound Visual Setup";
        private const string ApplyM9E1GameModesMenu = "Tools/PulseForge/UI/Apply M9E.1 Game Modes Setup";
        private const string ApplyM9E2SaboteurFogMenu = "Tools/PulseForge/UI/Apply M9E.2 Saboteur & Fog Setup";
        private const string ApplyM9F1ForecastMenu = "Tools/PulseForge/UI/Apply M9F.1 Forecast Setup";
        private const string ApplyM9F2GroupTimingMenu = "Tools/PulseForge/UI/Apply M9F.2 Group Timing Setup";
        private const string ApplyM9G1CoverageMenu = "Tools/PulseForge/UI/Apply M9G.1 Coverage Setup";
        private const string ApplyM9G2PlayabilityAssistMenu = "Tools/PulseForge/UI/Apply M9G.2 Playability Assist Setup";
        private const string ApplyM9HOnboardingTrainingMenu =
            "Tools/PulseForge/UI/Apply M9H Onboarding & Training Setup";
        private const string ApplyM10ABArenaEnemyVisualMenu =
            "Tools/PulseForge/UI/Apply M10AB Arena & Enemy Visual Setup";
        private const string ApplyM10CDVfxReactivePolishMenu =
            "Tools/PulseForge/UI/Apply M10CD VFX & Reactive Polish Setup";
        private const string ValidateMenu = "Tools/PulseForge/UI/Validate Scene UI";
        private const string ButtonMotionClassIdentifier =
            "Assembly-CSharp::PulseForge.Runtime.Unity.UI.PulseForgeUIButtonMotion";
        private const string ButtonMotionScriptPath =
            "Assets/PulseForge/Runtime/Unity/UI/PulseForgeUIButtonMotion.cs";

        [MenuItem(MaterializeMenu)]
        private static void Materialize()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "PulseForge UI",
                    "Scene UI can only be materialized in Edit Mode.",
                    "OK");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            DebugRhythmPrototypeController[] controllers = FindInActiveScene<DebugRhythmPrototypeController>(activeScene);
            if (controllers.Length != 1)
            {
                Debug.LogError(
                    controllers.Length == 0
                        ? "PulseForge UI materialization requires one DebugRhythmPrototypeController in the active scene. None was found."
                        : "PulseForge UI materialization requires exactly one DebugRhythmPrototypeController in the active scene. "
                            + controllers.Length + " were found.");
                return;
            }

            EventSystem[] existingEventSystems = FindInActiveScene<EventSystem>(activeScene);
            if (existingEventSystems.Length > 1)
            {
                Debug.LogError(
                    "PulseForge UI materialization stopped because multiple EventSystems exist in the active scene. "
                    + "Keep one EventSystem, then run the command again.");
                return;
            }

            PulseForgeSceneUIRoot[] existingRoots = FindInActiveScene<PulseForgeSceneUIRoot>(activeScene);
            if (existingRoots.Length > 0)
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "PulseForge Scene UI Already Exists",
                    existingRoots.Length == 1
                        ? "A PulseForge Scene UI root already exists."
                        : existingRoots.Length + " PulseForge Scene UI roots already exist.",
                    "Select Existing",
                    "Cancel",
                    "Replace Existing");
                if (choice == 0)
                {
                    Selection.activeGameObject = existingRoots[0].gameObject;
                    EditorGUIUtility.PingObject(existingRoots[0]);
                    return;
                }

                if (choice != 2)
                {
                    return;
                }
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Materialize PulseForge Scene UI");
            for (int i = 0; i < existingRoots.Length; i++)
            {
                Undo.DestroyObjectImmediate(existingRoots[i].gameObject);
            }

            PulseForgeSceneUIRoot root = PulseForgeUIFactory.CreateStaticHierarchy(null, "PulseForge UI");
            SceneManager.MoveGameObjectToScene(root.gameObject, activeScene);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create PulseForge Scene UI");

            EventSystem eventSystem = EnsureSingleInputSystemEventSystem(activeScene);
            Undo.RecordObject(root, "Assign PulseForge EventSystem");
            root.AssignEventSystem(eventSystem);
            root.ApplyVisibility(PulseForgeUIState.Setup);

            DebugRhythmPrototypeController controller = controllers[0];
            PulseForgeUIBootstrap bootstrap = controller.GetComponent<PulseForgeUIBootstrap>();
            if (bootstrap == null)
            {
                bootstrap = Undo.AddComponent<PulseForgeUIBootstrap>(controller.gameObject);
            }

            Undo.RecordObject(bootstrap, "Assign PulseForge Scene UI Root");
            bootstrap.AssignSceneUIRoot(root);
            EditorUtility.SetDirty(bootstrap);
            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = root.gameObject;
            EditorGUIUtility.PingObject(root);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log(
                "PulseForge Scene UI was materialized. The scene is dirty and has not been saved automatically.",
                root);
        }

        [MenuItem(ValidateMenu)]
        private static void Validate()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            List<string> errors = new List<string>();
            PulseForgeSceneUIRoot[] roots = FindInActiveScene<PulseForgeSceneUIRoot>(activeScene);
            if (roots.Length == 0)
            {
                errors.Add("PulseForgeSceneUIRoot was not found in the active scene.");
            }
            else if (roots.Length > 1)
            {
                errors.Add("Multiple PulseForgeSceneUIRoot components were found: " + roots.Length + ".");
            }

            for (int i = 0; i < roots.Length; i++)
            {
                roots[i].CollectValidationErrors(errors);
                ValidateCanvas(roots[i], errors);
            }

            DebugRhythmPrototypeController[] controllers = FindInActiveScene<DebugRhythmPrototypeController>(activeScene);
            if (controllers.Length != 1)
            {
                errors.Add("Expected one DebugRhythmPrototypeController; found " + controllers.Length + ".");
            }
            else
            {
                PulseForgeUIBootstrap bootstrap = controllers[0].GetComponent<PulseForgeUIBootstrap>();
                if (bootstrap == null)
                {
                    errors.Add("DebugRhythmPrototypeController has no PulseForgeUIBootstrap component.");
                }
                else if (roots.Length == 1 && bootstrap.SceneUIRoot != roots[0])
                {
                    errors.Add("PulseForgeUIBootstrap is not linked to the scene UI root.");
                }
            }

            EventSystem[] eventSystems = FindInActiveScene<EventSystem>(activeScene);
            if (eventSystems.Length != 1)
            {
                errors.Add("Expected one EventSystem; found " + eventSystems.Length + ".");
            }
            else if (eventSystems[0].GetComponent<InputSystemUIInputModule>() == null)
            {
                errors.Add("The scene EventSystem has no InputSystemUIInputModule.");
            }
            else if (eventSystems[0].GetComponents<BaseInputModule>().Length != 1)
            {
                errors.Add("The scene EventSystem must have exactly one BaseInputModule, using InputSystemUIInputModule.");
            }

            Canvas[] canvases = FindInActiveScene<Canvas>(activeScene);
            if (canvases.Length > 1)
            {
                errors.Add("Multiple Canvas components were found in the active scene: " + canvases.Length + ".");
            }

            if (errors.Count == 0)
            {
                Debug.Log("PulseForge Scene UI validation passed: no issues found.", roots[0]);
                return;
            }

            Debug.LogError("PulseForge Scene UI validation failed:\n- " + string.Join("\n- ", errors));
        }

        [MenuItem(ApplyM8AStyleMenu)]
        private static void ApplyM8AVisualStyle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "PulseForge UI",
                    "M8A visual style can only be applied in Edit Mode.",
                    "OK");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            PulseForgeSceneUIRoot[] roots = FindInActiveScene<PulseForgeSceneUIRoot>(activeScene);
            if (roots.Length != 1)
            {
                Debug.LogError(
                    roots.Length == 0
                        ? "Apply M8A Visual Style requires one PulseForgeSceneUIRoot in the active scene. None was found."
                        : "Apply M8A Visual Style requires exactly one PulseForgeSceneUIRoot in the active scene. "
                            + roots.Length + " were found.");
                return;
            }

            PulseForgeSceneUIRoot root = roots[0];
            Transform existingBackgroundLayers = root.Background == null
                ? null
                : root.Background.transform.Find(PulseForgeUIVisualStyle.BackgroundLayersName);
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply PulseForge M8A Visual Style");
            Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Apply PulseForge M8A Visual Style");

            PulseForgeUIVisualStyle.Apply(root);
            if (existingBackgroundLayers == null && root.Background != null)
            {
                Transform createdLayers = root.Background.transform.Find(PulseForgeUIVisualStyle.BackgroundLayersName);
                if (createdLayers != null)
                {
                    Undo.RegisterCreatedObjectUndo(createdLayers.gameObject, "Create PulseForge M8A Background Layers");
                }
            }

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    EditorUtility.SetDirty(components[i]);
                }
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = root.gameObject;
            EditorGUIUtility.PingObject(root);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log(
                "PulseForge M8A visual style was applied. The scene is dirty and has not been saved automatically.",
                root);
        }

        [MenuItem(ApplyM8B1MotionMenu)]
        private static void ApplyM8B1MotionSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "PulseForge UI",
                    "M8B.1 motion setup can only be applied in Edit Mode.",
                    "OK");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            PulseForgeSceneUIRoot[] roots = FindInActiveScene<PulseForgeSceneUIRoot>(activeScene);
            if (roots.Length != 1)
            {
                Debug.LogError(
                    roots.Length == 0
                        ? "Apply M8B.1 Motion Setup requires one PulseForgeSceneUIRoot in the active scene. None was found."
                        : "Apply M8B.1 Motion Setup requires exactly one PulseForgeSceneUIRoot in the active scene. "
                            + roots.Length + " were found.");
                return;
            }

            PulseForgeSceneUIRoot root = roots[0];
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply PulseForge M8B.1 Motion Setup");
            Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Apply PulseForge M8B.1 Motion Setup");
            Undo.RecordObject(root, "Configure PulseForge M8B.1 Motion");

            int removedInvalidButtonMotionComponents = RemoveInvalidButtonMotionComponents(root);
            int removedButtonMotionComponents = RemoveButtonMotionComponents(root);
            int removedMissingScripts = RemoveMissingMonoBehaviours(root.gameObject);
            PulseForgeUIMotionSetup.Apply(
                root,
                (gameObject, componentType) => Undo.AddComponent(gameObject, componentType));

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    EditorUtility.SetDirty(components[i]);
                }
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = root.gameObject;
            EditorGUIUtility.PingObject(root);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log(
                "PulseForge M8B.1 motion setup was applied. Removed "
                    + removedInvalidButtonMotionComponents
                    + " invalid button motion component(s), "
                    + removedButtonMotionComponents
                    + " stale valid button motion component(s), and "
                    + removedMissingScripts
                    + " missing script component(s). The scene is dirty and has not been saved automatically.",
                root);
        }

        private static int RemoveInvalidButtonMotionComponents(PulseForgeSceneUIRoot root)
        {
            if (root == null)
            {
                return 0;
            }

            List<Component> invalidComponents = new List<Component>();
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                SerializedObject gameObjectData = new SerializedObject(transforms[i].gameObject);
                SerializedProperty components = gameObjectData.FindProperty("m_Component");
                for (int componentIndex = 0; componentIndex < components.arraySize; componentIndex++)
                {
                    SerializedProperty componentReference = components
                        .GetArrayElementAtIndex(componentIndex)
                        .FindPropertyRelative("component");
                    Component component = componentReference.objectReferenceValue as Component;
                    if (component == null)
                    {
                        continue;
                    }

                    SerializedObject componentData = new SerializedObject(component);
                    SerializedProperty classIdentifier = componentData.FindProperty("m_EditorClassIdentifier");
                    if (classIdentifier == null
                        || classIdentifier.stringValue != ButtonMotionClassIdentifier)
                    {
                        continue;
                    }

                    SerializedProperty script = componentData.FindProperty("m_Script");
                    string scriptPath = script == null
                        ? string.Empty
                        : AssetDatabase.GetAssetPath(script.objectReferenceValue);
                    if (scriptPath == ButtonMotionScriptPath)
                    {
                        continue;
                    }

                    invalidComponents.Add(component);
                }
            }

            for (int i = 0; i < invalidComponents.Count; i++)
            {
                Undo.DestroyObjectImmediate(invalidComponents[i]);
            }

            return invalidComponents.Count;
        }

        private static int RemoveButtonMotionComponents(PulseForgeSceneUIRoot root)
        {
            if (root == null)
            {
                return 0;
            }

            PulseForgeUIButtonMotion[] components =
                root.GetComponentsInChildren<PulseForgeUIButtonMotion>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Undo.DestroyObjectImmediate(components[i]);
            }

            return components.Length;
        }

        private static int RemoveMissingMonoBehaviours(GameObject root)
        {
            if (root == null)
            {
                return 0;
            }

            int removedCount = 0;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                GameObject target = transforms[i].gameObject;
                int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(target);
                if (missingCount == 0)
                {
                    continue;
                }

                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);
                EditorUtility.SetDirty(target);
                removedCount += missingCount;
            }

            return removedCount;
        }

        [MenuItem(ApplyM8B2FeedbackMenu)]
        private static void ApplyM8B2GameplayFeedbackSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "PulseForge UI",
                    "M8B.2 gameplay feedback setup can only be applied in Edit Mode.",
                    "OK");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            PulseForgeSceneUIRoot[] roots = FindInActiveScene<PulseForgeSceneUIRoot>(activeScene);
            if (roots.Length != 1)
            {
                Debug.LogError(
                    roots.Length == 0
                        ? "Apply M8B.2 Gameplay Feedback Setup requires one PulseForgeSceneUIRoot in the active scene. None was found."
                        : "Apply M8B.2 Gameplay Feedback Setup requires exactly one PulseForgeSceneUIRoot in the active scene. "
                            + roots.Length + " were found.");
                return;
            }

            PulseForgeSceneUIRoot root = roots[0];
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply PulseForge M8B.2 Gameplay Feedback Setup");
            Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Apply PulseForge M8B.2 Gameplay Feedback Setup");
            Undo.RecordObject(root, "Configure PulseForge M8B.2 Gameplay Feedback");

            PulseForgeGameplayFeedbackSetup.Apply(
                root,
                (gameObject, componentType) => Undo.AddComponent(gameObject, componentType));

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    EditorUtility.SetDirty(components[i]);
                }
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = root.gameObject;
            EditorGUIUtility.PingObject(root);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log(
                "PulseForge M8B.2 gameplay feedback setup was applied. The scene is dirty and has not been saved automatically.",
                root);
        }

        [MenuItem(ApplyM8CPersistenceMenu)]
        private static void ApplyM8CPersistenceUISetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "PulseForge UI",
                    "M8C persistence UI setup can only be applied in Edit Mode.",
                    "OK");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            PulseForgeSceneUIRoot[] roots = FindInActiveScene<PulseForgeSceneUIRoot>(activeScene);
            if (roots.Length != 1)
            {
                Debug.LogError(
                    roots.Length == 0
                        ? "Apply M8C Persistence UI Setup requires one PulseForgeSceneUIRoot in the active scene. None was found."
                        : "Apply M8C Persistence UI Setup requires exactly one PulseForgeSceneUIRoot in the active scene. "
                            + roots.Length + " were found.");
                return;
            }

            PulseForgeSceneUIRoot root = roots[0];
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply PulseForge M8C Persistence UI Setup");
            Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Apply PulseForge M8C Persistence UI Setup");
            Undo.RecordObject(root, "Configure PulseForge M8C Persistence UI");

            PulseForgePersistenceUISetup.Apply(
                root,
                gameObject => Undo.RegisterCreatedObjectUndo(gameObject, "Create PulseForge M8C UI"));
            PulseForgeUIMotionSetup.Apply(
                root,
                (gameObject, componentType) => Undo.AddComponent(gameObject, componentType));

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    EditorUtility.SetDirty(components[i]);
                }
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = root.gameObject;
            EditorGUIUtility.PingObject(root);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log(
                "PulseForge M8C persistence UI setup was applied. The scene is dirty and has not been saved automatically.",
                root);
        }

        [MenuItem(ApplyM8DSettingsMenu)]
        private static void ApplyM8DSettingsUISetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "PulseForge UI",
                    "M8D Settings UI setup can only be applied in Edit Mode.",
                    "OK");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            PulseForgeSceneUIRoot[] roots = FindInActiveScene<PulseForgeSceneUIRoot>(activeScene);
            if (roots.Length != 1)
            {
                Debug.LogError(
                    roots.Length == 0
                        ? "Apply M8D Settings UI Setup requires one PulseForgeSceneUIRoot in the active scene. None was found."
                        : "Apply M8D Settings UI Setup requires exactly one PulseForgeSceneUIRoot in the active scene. "
                            + roots.Length + " were found.");
                return;
            }

            PulseForgeSceneUIRoot root = roots[0];
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply PulseForge M8D Settings UI Setup");
            Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Apply PulseForge M8D Settings UI Setup");
            Undo.RecordObject(root, "Configure PulseForge M8D Settings UI");
            PulseForgeSettingsUISetup.Apply(
                root,
                gameObject => Undo.RegisterCreatedObjectUndo(gameObject, "Create PulseForge M8D UI"));
            PulseForgeUIMotionSetup.Apply(
                root,
                (gameObject, componentType) => Undo.AddComponent(gameObject, componentType));

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null) EditorUtility.SetDirty(components[i]);
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = root.gameObject;
            EditorGUIUtility.PingObject(root);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log(
                "PulseForge M8D Settings UI setup was applied. The scene is dirty and has not been saved automatically.",
                root);
        }

        [MenuItem(ApplyM9D1RadialStageMenu)]
        private static void ApplyM9D1RadialStageSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "PulseForge UI",
                    "M9D.1 radial stage setup can only be applied in Edit Mode.",
                    "OK");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            PulseForgeSceneUIRoot[] roots = FindInActiveScene<PulseForgeSceneUIRoot>(activeScene);
            if (roots.Length != 1)
            {
                Debug.LogError(
                    roots.Length == 0
                        ? "Apply M9D.1 Radial Stage Setup requires one PulseForgeSceneUIRoot in the active scene. None was found."
                        : "Apply M9D.1 Radial Stage Setup requires exactly one PulseForgeSceneUIRoot in the active scene. "
                            + roots.Length + " were found.");
                return;
            }

            PulseForgeSceneUIRoot root = roots[0];
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply PulseForge M9D.1 Radial Stage Setup");
            Undo.RegisterFullObjectHierarchyUndo(
                root.gameObject,
                "Apply PulseForge M9D.1 Radial Stage Setup");
            Undo.RecordObject(root, "Configure PulseForge M9D.1 Radial Stage");

            RadialCombatStageView stage = RadialCombatStageSetup.Apply(
                root,
                gameObject => Undo.RegisterCreatedObjectUndo(
                    gameObject,
                    "Create PulseForge M9D.1 Radial Stage UI"),
                (gameObject, componentType) => Undo.AddComponent(gameObject, componentType));

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    EditorUtility.SetDirty(components[i]);
                }
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = stage == null ? root.gameObject : stage.gameObject;
            EditorGUIUtility.PingObject(stage == null ? (Object)root : stage);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log(
                "PulseForge M9D.1 radial stage setup was applied. The scene is dirty and has not been saved automatically.",
                root);
        }

        [MenuItem(ApplyM9D2CompoundVisualMenu)]
        private static void ApplyM9D2CompoundVisualSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "PulseForge UI",
                    "M9D.2 compound visual setup can only be applied in Edit Mode.",
                    "OK");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            PulseForgeSceneUIRoot[] roots = FindInActiveScene<PulseForgeSceneUIRoot>(activeScene);
            if (roots.Length != 1)
            {
                Debug.LogError(
                    roots.Length == 0
                        ? "Apply M9D.2 Compound Visual Setup requires one PulseForgeSceneUIRoot in the active scene. None was found."
                        : "Apply M9D.2 Compound Visual Setup requires exactly one PulseForgeSceneUIRoot in the active scene. "
                            + roots.Length + " were found.");
                return;
            }

            PulseForgeSceneUIRoot root = roots[0];
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply PulseForge M9D.2 Compound Visual Setup");
            Undo.RegisterFullObjectHierarchyUndo(
                root.gameObject,
                "Apply PulseForge M9D.2 Compound Visual Setup");
            Undo.RecordObject(root, "Configure PulseForge M9D.2 Compound Visuals");

            RadialCombatStageView stage = RadialCompoundVisualSetup.Apply(
                root,
                gameObject => Undo.RegisterCreatedObjectUndo(
                    gameObject,
                    "Create PulseForge M9D.2 Compound Visual UI"),
                (gameObject, componentType) => Undo.AddComponent(gameObject, componentType));

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    EditorUtility.SetDirty(components[i]);
                }
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = stage == null ? root.gameObject : stage.gameObject;
            EditorGUIUtility.PingObject(stage == null ? (Object)root : stage);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log(
                "PulseForge M9D.2 compound visual setup was applied. The scene is dirty and has not been saved automatically.",
                root);
        }

        [MenuItem(ApplyM9E1GameModesMenu)]
        private static void ApplyM9E1GameModesSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "PulseForge UI",
                    "M9E.1 game modes setup can only be applied in Edit Mode.",
                    "OK");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            PulseForgeSceneUIRoot[] roots = FindInActiveScene<PulseForgeSceneUIRoot>(activeScene);
            if (roots.Length != 1)
            {
                Debug.LogError(
                    roots.Length == 0
                        ? "Apply M9E.1 Game Modes Setup requires one PulseForgeSceneUIRoot in the active scene. None was found."
                        : "Apply M9E.1 Game Modes Setup requires exactly one PulseForgeSceneUIRoot in the active scene. "
                            + roots.Length + " were found.");
                return;
            }

            PulseForgeSceneUIRoot root = roots[0];
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply PulseForge M9E.1 Game Modes Setup");
            Undo.RegisterFullObjectHierarchyUndo(
                root.gameObject,
                "Apply PulseForge M9E.1 Game Modes Setup");
            Undo.RecordObject(root, "Configure PulseForge M9E.1 Game Modes UI");
            PulseForgeGameModesUISetup.Apply(
                root,
                gameObject =>
                {
                    if (gameObject != null)
                    {
                        Undo.RegisterCreatedObjectUndo(
                            gameObject,
                            "Create PulseForge M9E.1 Game Modes UI");
                    }
                });

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    EditorUtility.SetDirty(components[i]);
                }
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = root.gameObject;
            EditorGUIUtility.PingObject(root);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log(
                "PulseForge M9E.1 game modes setup was applied. The scene is dirty and has not been saved automatically.",
                root);
        }

        [MenuItem(ApplyM9E2SaboteurFogMenu)]
        private static void ApplyM9E2SaboteurFogSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "PulseForge UI",
                    "M9E.2 Saboteur & Fog setup can only be applied in Edit Mode.",
                    "OK");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            PulseForgeSceneUIRoot[] roots = FindInActiveScene<PulseForgeSceneUIRoot>(activeScene);
            if (roots.Length != 1)
            {
                Debug.LogError(
                    roots.Length == 0
                        ? "Apply M9E.2 Saboteur & Fog Setup requires one PulseForgeSceneUIRoot in the active scene. None was found."
                        : "Apply M9E.2 Saboteur & Fog Setup requires exactly one PulseForgeSceneUIRoot in the active scene. "
                            + roots.Length + " were found.");
                return;
            }

            PulseForgeSceneUIRoot root = roots[0];
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply PulseForge M9E.2 Saboteur & Fog Setup");
            Undo.RegisterFullObjectHierarchyUndo(
                root.gameObject,
                "Apply PulseForge M9E.2 Saboteur & Fog Setup");
            Undo.RecordObject(root, "Configure PulseForge M9E.2 Saboteur & Fog UI");

            RadialCombatStageView stage = RadialSaboteurFogSetup.Apply(
                root,
                gameObject => Undo.RegisterCreatedObjectUndo(
                    gameObject,
                    "Create PulseForge M9E.2 Saboteur & Fog UI"),
                (gameObject, componentType) => Undo.AddComponent(gameObject, componentType));

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    EditorUtility.SetDirty(components[i]);
                }
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = stage == null ? root.gameObject : stage.gameObject;
            EditorGUIUtility.PingObject(stage == null ? (Object)root : stage);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log(
                "PulseForge M9E.2 Saboteur & Fog setup was applied. The scene is dirty and has not been saved automatically.",
                root);
        }

        [MenuItem(ApplyM9HOnboardingTrainingMenu)]
        private static void ApplyM9HOnboardingTrainingSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "PulseForge UI",
                    "M9H Onboarding & Training setup can only be applied in Edit Mode.",
                    "OK");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            PulseForgeSceneUIRoot[] roots = FindInActiveScene<PulseForgeSceneUIRoot>(activeScene);
            if (roots.Length != 1)
            {
                Debug.LogError(
                    roots.Length == 0
                        ? "Apply M9H Onboarding & Training Setup requires one PulseForgeSceneUIRoot in the active scene. None was found."
                        : "Apply M9H Onboarding & Training Setup requires exactly one PulseForgeSceneUIRoot in the active scene. "
                            + roots.Length + " were found.");
                return;
            }

            PulseForgeSceneUIRoot root = roots[0];
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply PulseForge M9H Onboarding & Training Setup");
            Undo.RegisterFullObjectHierarchyUndo(
                root.gameObject,
                "Apply PulseForge M9H Onboarding & Training Setup");
            Undo.RecordObject(root, "Configure PulseForge M9H UI");
            PulseForgeM9HUISetup.Apply(
                root,
                gameObject => Undo.RegisterCreatedObjectUndo(
                    gameObject,
                    "Create PulseForge M9H Onboarding & Training UI"));

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    EditorUtility.SetDirty(components[i]);
                }
            }

            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = root.gameObject;
            EditorGUIUtility.PingObject(root);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log(
                "PulseForge M9H Onboarding & Training setup was applied. The scene is dirty and has not been saved automatically.",
                root);
        }

        [MenuItem(ApplyM10ABArenaEnemyVisualMenu)]
        private static void ApplyM10ABArenaEnemyVisualSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "PulseForge UI",
                    "M10AB Arena & Enemy Visual setup can only be applied in Edit Mode.",
                    "OK");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            PulseForgeSceneUIRoot[] roots = FindInActiveScene<PulseForgeSceneUIRoot>(activeScene);
            if (roots.Length != 1)
            {
                Debug.LogError(
                    roots.Length == 0
                        ? "Apply M10AB Arena & Enemy Visual Setup requires one PulseForgeSceneUIRoot in the active scene. None was found."
                        : "Apply M10AB Arena & Enemy Visual Setup requires exactly one PulseForgeSceneUIRoot in the active scene. "
                            + roots.Length + " were found.");
                return;
            }

            PulseForgeSceneUIRoot root = roots[0];
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply PulseForge M10AB Arena & Enemy Visual Setup");
            Undo.RegisterFullObjectHierarchyUndo(
                root.gameObject,
                "Apply PulseForge M10AB Arena & Enemy Visual Setup");
            Undo.RecordObject(root, "Configure PulseForge M10AB Arena & Enemy Visuals");

            RadialCombatStageView stage = RadialArenaVisualSetup.Apply(
                root,
                gameObject => Undo.RegisterCreatedObjectUndo(
                    gameObject,
                    "Create PulseForge M10AB Arena & Enemy Visual UI"),
                (gameObject, componentType) => Undo.AddComponent(gameObject, componentType));

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    EditorUtility.SetDirty(components[i]);
                }
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = stage == null ? root.gameObject : stage.gameObject;
            EditorGUIUtility.PingObject(stage == null ? (Object)root : stage);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log(
                "PulseForge M10AB Arena & Enemy Visual setup was applied. The scene is dirty and has not been saved automatically.",
                root);
        }

        [MenuItem(ApplyM10CDVfxReactivePolishMenu)]
        private static void ApplyM10CDVfxReactivePolishSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "PulseForge UI",
                    "M10CD VFX & Reactive Polish setup can only be applied in Edit Mode.",
                    "OK");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            PulseForgeSceneUIRoot[] roots = FindInActiveScene<PulseForgeSceneUIRoot>(activeScene);
            if (roots.Length != 1)
            {
                Debug.LogError(
                    roots.Length == 0
                        ? "Apply M10CD VFX & Reactive Polish Setup requires one PulseForgeSceneUIRoot in the active scene. None was found."
                        : "Apply M10CD VFX & Reactive Polish Setup requires exactly one PulseForgeSceneUIRoot in the active scene. "
                            + roots.Length + " were found.");
                return;
            }

            PulseForgeSceneUIRoot root = roots[0];
            RadialCombatStageView existingStage = root.RadialCombatStage;
            if (existingStage == null || existingStage.ArenaGraphic == null)
            {
                Debug.LogError(
                    "Apply M10CD VFX & Reactive Polish Setup requires the existing M10AB arena setup. "
                    + "Apply M10AB first; M10CD will not recreate it.",
                    root);
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply PulseForge M10CD VFX & Reactive Polish Setup");
            Undo.RegisterFullObjectHierarchyUndo(
                root.gameObject,
                "Apply PulseForge M10CD VFX & Reactive Polish Setup");
            Undo.RecordObject(root, "Configure PulseForge M10CD VFX & Reactive Polish");

            RadialCombatStageView stage = RadialCombatVfxSetup.Apply(
                root,
                gameObject => Undo.RegisterCreatedObjectUndo(
                    gameObject,
                    "Create PulseForge M10CD VFX & Reactive Polish UI"),
                (gameObject, componentType) => Undo.AddComponent(gameObject, componentType));

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    EditorUtility.SetDirty(components[i]);
                }
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = stage == null ? root.gameObject : stage.gameObject;
            EditorGUIUtility.PingObject(stage == null ? (Object)root : stage);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log(
                "PulseForge M10CD VFX & Reactive Polish setup was applied. The scene is dirty and has not been saved automatically.",
                root);
        }

        [MenuItem(ApplyM9F1ForecastMenu)]
        private static void ApplyM9F1ForecastSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "PulseForge UI",
                    "M9F.1 Forecast setup can only be applied in Edit Mode.",
                    "OK");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            PulseForgeSceneUIRoot[] roots = FindInActiveScene<PulseForgeSceneUIRoot>(activeScene);
            if (roots.Length != 1)
            {
                Debug.LogError(
                    roots.Length == 0
                        ? "Apply M9F.1 Forecast Setup requires one PulseForgeSceneUIRoot in the active scene. None was found."
                        : "Apply M9F.1 Forecast Setup requires exactly one PulseForgeSceneUIRoot in the active scene. "
                            + roots.Length + " were found.");
                return;
            }

            PulseForgeSceneUIRoot root = roots[0];
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply PulseForge M9F.1 Forecast Setup");
            Undo.RegisterFullObjectHierarchyUndo(
                root.gameObject,
                "Apply PulseForge M9F.1 Forecast Setup");
            Undo.RecordObject(root, "Configure PulseForge M9F.1 Forecast UI");

            RadialCombatStageView stage = RadialForecastSetup.Apply(
                root,
                gameObject => Undo.RegisterCreatedObjectUndo(
                    gameObject,
                    "Create PulseForge M9F.1 Forecast UI"),
                (gameObject, componentType) => Undo.AddComponent(gameObject, componentType));

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    EditorUtility.SetDirty(components[i]);
                }
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = stage == null ? root.gameObject : stage.gameObject;
            EditorGUIUtility.PingObject(stage == null ? (Object)root : stage);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log(
                "PulseForge M9F.1 Forecast setup was applied. The scene is dirty and has not been saved automatically.",
                root);
        }

        [MenuItem(ApplyM9F2GroupTimingMenu)]
        private static void ApplyM9F2GroupTimingSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "PulseForge UI",
                    "M9F.2 Group Timing setup can only be applied in Edit Mode.",
                    "OK");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            PulseForgeSceneUIRoot[] roots = FindInActiveScene<PulseForgeSceneUIRoot>(activeScene);
            if (roots.Length != 1)
            {
                Debug.LogError(
                    roots.Length == 0
                        ? "Apply M9F.2 Group Timing Setup requires one PulseForgeSceneUIRoot in the active scene. None was found."
                        : "Apply M9F.2 Group Timing Setup requires exactly one PulseForgeSceneUIRoot in the active scene. "
                            + roots.Length + " were found.");
                return;
            }

            PulseForgeSceneUIRoot root = roots[0];
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply PulseForge M9F.2 Group Timing Setup");
            Undo.RegisterFullObjectHierarchyUndo(
                root.gameObject,
                "Apply PulseForge M9F.2 Group Timing Setup");
            Undo.RecordObject(root, "Configure PulseForge M9F.2 Group Timing UI");

            RadialCombatStageView stage = RadialGroupTimingSetup.Apply(
                root,
                gameObject => Undo.RegisterCreatedObjectUndo(
                    gameObject,
                    "Create PulseForge M9F.2 Group Timing UI"),
                (gameObject, componentType) => Undo.AddComponent(gameObject, componentType));

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    EditorUtility.SetDirty(components[i]);
                }
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = stage == null ? root.gameObject : stage.gameObject;
            EditorGUIUtility.PingObject(stage == null ? (Object)root : stage);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log(
                "PulseForge M9F.2 Group Timing setup was applied. The scene is dirty and has not been saved automatically.",
                root);
        }

        [MenuItem(ApplyM9G1CoverageMenu)]
        private static void ApplyM9G1CoverageSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "PulseForge UI",
                    "M9G.1 Coverage setup can only be applied in Edit Mode.",
                    "OK");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            PulseForgeSceneUIRoot[] roots = FindInActiveScene<PulseForgeSceneUIRoot>(activeScene);
            if (roots.Length != 1)
            {
                Debug.LogError(
                    roots.Length == 0
                        ? "Apply M9G.1 Coverage Setup requires one PulseForgeSceneUIRoot in the active scene. None was found."
                        : "Apply M9G.1 Coverage Setup requires exactly one PulseForgeSceneUIRoot in the active scene. "
                            + roots.Length + " were found.");
                return;
            }

            PulseForgeSceneUIRoot root = roots[0];
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply PulseForge M9G.1 Coverage Setup");
            Undo.RegisterFullObjectHierarchyUndo(
                root.gameObject,
                "Apply PulseForge M9G.1 Coverage Setup");
            Undo.RecordObject(root, "Configure PulseForge M9G.1 Coverage UI");
            PulseForgeCoverageUISetup.Apply(
                root,
                gameObject =>
                {
                    if (gameObject != null)
                    {
                        Undo.RegisterCreatedObjectUndo(
                            gameObject,
                            "Create PulseForge M9G.1 Coverage UI");
                    }
                });

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    EditorUtility.SetDirty(components[i]);
                }
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = root.gameObject;
            EditorGUIUtility.PingObject(root);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log(
                "PulseForge M9G.1 Coverage setup was applied. The scene is dirty and has not been saved automatically.",
                root);
        }

        [MenuItem(ApplyM9G2PlayabilityAssistMenu)]
        private static void ApplyM9G2PlayabilityAssistSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "PulseForge UI",
                    "M9G.2 Playability Assist setup can only be applied in Edit Mode.",
                    "OK");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            PulseForgeSceneUIRoot[] roots = FindInActiveScene<PulseForgeSceneUIRoot>(activeScene);
            if (roots.Length != 1)
            {
                Debug.LogError(
                    roots.Length == 0
                        ? "Apply M9G.2 Playability Assist Setup requires one PulseForgeSceneUIRoot in the active scene. None was found."
                        : "Apply M9G.2 Playability Assist Setup requires exactly one PulseForgeSceneUIRoot in the active scene. "
                            + roots.Length + " were found.");
                return;
            }

            PulseForgeSceneUIRoot root = roots[0];
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply PulseForge M9G.2 Playability Assist Setup");
            Undo.RegisterFullObjectHierarchyUndo(
                root.gameObject,
                "Apply PulseForge M9G.2 Playability Assist Setup");
            Undo.RecordObject(root, "Configure PulseForge M9G.2 Playability Assist UI");
            PulseForgePlayabilityAssistUISetup.Apply(
                root,
                gameObject =>
                {
                    if (gameObject != null)
                    {
                        Undo.RegisterCreatedObjectUndo(
                            gameObject,
                            "Create PulseForge M9G.2 Playability Assist UI");
                    }
                });

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    EditorUtility.SetDirty(components[i]);
                }
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = root.gameObject;
            EditorGUIUtility.PingObject(root);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log(
                "PulseForge M9G.2 Playability Assist setup was applied. The scene is dirty and has not been saved automatically.",
                root);
        }

        private static EventSystem EnsureSingleInputSystemEventSystem(Scene scene)
        {
            EventSystem[] eventSystems = FindInActiveScene<EventSystem>(scene);
            EventSystem eventSystem;
            if (eventSystems.Length == 0)
            {
                eventSystem = PulseForgeUIFactory.CreateEventSystem();
                SceneManager.MoveGameObjectToScene(eventSystem.gameObject, scene);
                Undo.RegisterCreatedObjectUndo(eventSystem.gameObject, "Create PulseForge EventSystem");
            }
            else
            {
                eventSystem = eventSystems[0];
            }

            BaseInputModule[] existingModules = eventSystem.GetComponents<BaseInputModule>();
            for (int i = 0; i < existingModules.Length; i++)
            {
                if (!(existingModules[i] is InputSystemUIInputModule))
                {
                    Undo.DestroyObjectImmediate(existingModules[i]);
                }
            }

            InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = Undo.AddComponent<InputSystemUIInputModule>(eventSystem.gameObject);
            }

            if (inputModule.actionsAsset == null)
            {
                Undo.RecordObject(inputModule, "Assign Input System UI Actions");
                inputModule.AssignDefaultActions();
                EditorUtility.SetDirty(inputModule);
            }
            return eventSystem;
        }

        private static void ValidateCanvas(PulseForgeSceneUIRoot root, List<string> errors)
        {
            if (root.Canvas == null)
            {
                return;
            }

            CanvasScaler scaler = root.Canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                errors.Add("CanvasScaler is missing.");
                return;
            }

            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                errors.Add("CanvasScaler must use Scale With Screen Size.");
            }

            if (scaler.referenceResolution != new Vector2(1920f, 1080f))
            {
                errors.Add("CanvasScaler reference resolution must be 1920x1080.");
            }

            if (!Mathf.Approximately(scaler.matchWidthOrHeight, 0.5f))
            {
                errors.Add("CanvasScaler Match must be 0.5.");
            }
        }

        internal static T[] FindInActiveScene<T>(Scene scene) where T : Component
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(component => component.gameObject.scene == scene)
                .ToArray();
        }
    }

    [CustomEditor(typeof(PulseForgeSceneUIRoot))]
    internal sealed class PulseForgeSceneUIRootInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Edit Mode UI Preview", EditorStyles.boldLabel);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Preview controls are disabled in Play Mode.", MessageType.Info);
                return;
            }

            PulseForgeSceneUIRoot root = (PulseForgeSceneUIRoot)target;
            DrawStateButton(root, "Setup", PulseForgeUIState.Setup);
            DrawStateButton(root, "Processing", PulseForgeUIState.Processing);
            DrawStateButton(root, "Ready", PulseForgeUIState.Ready);
            DrawStateButton(root, "Countdown", PulseForgeUIState.Countdown);
            DrawStateButton(root, "Playing", PulseForgeUIState.Playing);
            DrawStateButton(root, "Paused", PulseForgeUIState.Paused);
            DrawStateButton(root, "Completed", PulseForgeUIState.Completed);
            DrawStateButton(root, "Failed", PulseForgeUIState.Failed);
            DrawStateButton(root, "Error", PulseForgeUIState.Error);

            if (GUILayout.Button("Show All"))
            {
                ApplyPreview(root, null, true);
            }

            if (GUILayout.Button("Reset to Setup"))
            {
                ApplyPreview(root, PulseForgeUIState.Setup, false);
            }
        }

        private static void DrawStateButton(PulseForgeSceneUIRoot root, string label, PulseForgeUIState state)
        {
            if (GUILayout.Button(label))
            {
                ApplyPreview(root, state, false);
            }
        }

        private static void ApplyPreview(PulseForgeSceneUIRoot root, PulseForgeUIState? state, bool showAll)
        {
            List<Object> undoTargets = new List<Object> { root };
            AddPanel(undoTargets, root.SetupPanel);
            AddPanel(undoTargets, root.SavedTracksPanel);
            AddPanel(undoTargets, root.ProcessingPanel);
            AddPanel(undoTargets, root.ReadyPanel);
            AddPanel(undoTargets, root.GameplayHud);
            AddPanel(undoTargets, root.CountdownOverlay);
            AddPanel(undoTargets, root.PauseOverlay);
            AddPanel(undoTargets, root.ResultsPanel);
            AddPanel(undoTargets, root.ErrorPanel);
            Undo.RecordObjects(undoTargets.ToArray(), "Preview PulseForge UI State");

            if (showAll)
            {
                root.ShowAllPanels();
            }
            else if (state.HasValue)
            {
                root.ApplyVisibility(state.Value);
            }

            bool showCombat = showAll
                || state == PulseForgeUIState.Countdown
                || state == PulseForgeUIState.Playing
                || state == PulseForgeUIState.Paused
                || state == PulseForgeUIState.Failed;
            SetExistingCombatVisualRoot(root.gameObject.scene, showCombat);
            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        private static void AddPanel(List<Object> targets, PulseForgePanelView panel)
        {
            if (panel != null && panel.PanelRoot != null)
            {
                targets.Add(panel.PanelRoot);
            }
        }

        private static void SetExistingCombatVisualRoot(Scene scene, bool isVisible)
        {
            DebugCombatSceneView[] combatViews = PulseForgeSceneUIMaterializer.FindInActiveScene<DebugCombatSceneView>(scene);
            for (int i = 0; i < combatViews.Length; i++)
            {
                Transform visualRoot = combatViews[i].transform.Find("Combat Visual Root");
                if (visualRoot == null)
                {
                    continue;
                }

                Undo.RecordObject(visualRoot.gameObject, "Preview Combat Visual Root");
                visualRoot.gameObject.SetActive(isVisible);
                EditorUtility.SetDirty(visualRoot.gameObject);
            }
        }
    }
}
