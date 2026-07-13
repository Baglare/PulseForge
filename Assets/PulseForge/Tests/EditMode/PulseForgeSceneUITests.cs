using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PulseForge.Tests.EditMode
{
    public sealed class PulseForgeSceneUITests
    {
        private const string FactoryTypeName = "PulseForge.Runtime.Unity.UI.PulseForgeUIFactory, Assembly-CSharp";
        private const string RootTypeName = "PulseForge.Runtime.Unity.UI.PulseForgeSceneUIRoot, Assembly-CSharp";
        private const string BootstrapTypeName = "PulseForge.Runtime.Unity.UI.PulseForgeUIBootstrap, Assembly-CSharp";
        private const string ControllerTypeName =
            "PulseForge.Runtime.Unity.Prototype.DebugRhythmPrototypeController, Assembly-CSharp";

        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
            DestroyNamedRoot("PulseForge Runtime UI");
            DestroyNamedRoot("PulseForge EventSystem");
        }

        [Test]
        public void SceneRootPresent_BootstrapDoesNotCreateSecondCanvas()
        {
            Component root = CreateStaticHierarchy("Scene Authored PulseForge UI");
            Component controller = CreateComponent(ControllerTypeName, "PulseForge Controller");
            Component bootstrap = controller.gameObject.AddComponent(Type.GetType(BootstrapTypeName, true));
            Invoke(bootstrap, "AssignSceneUIRoot", root);
            int canvasCountBefore = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length;

            Invoke(bootstrap, "Ensure", controller);

            int canvasCountAfter = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length;
            Assert.That(canvasCountAfter, Is.EqualTo(canvasCountBefore));
            Assert.That(ReadProperty(bootstrap, "UsedRuntimeFallback"), Is.EqualTo(false));
        }

        [Test]
        public void SceneRootMissing_BootstrapCreatesRuntimeFallback()
        {
            Component controller = CreateComponent(ControllerTypeName, "PulseForge Fallback Controller");
            Component bootstrap = controller.gameObject.AddComponent(Type.GetType(BootstrapTypeName, true));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Runtime UI fallback was created"));

            object uiController = Invoke(bootstrap, "Ensure", controller);

            Assert.That(uiController, Is.Not.Null);
            Assert.That(GameObject.Find("PulseForge Runtime UI"), Is.Not.Null);
            Assert.That(ReadProperty(bootstrap, "UsedRuntimeFallback"), Is.EqualTo(true));
        }

        [Test]
        public void SceneRootValidation_ReportsMissingSerializedReference()
        {
            Component root = CreateStaticHierarchy("Validation UI");
            FieldInfo setupField = Type.GetType(RootTypeName, true).GetField(
                "setupPanel",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(setupField, Is.Not.Null);
            setupField.SetValue(root, null);
            List<string> errors = new List<string>();

            Invoke(root, "CollectValidationErrors", errors);

            Assert.That(errors, Has.Some.Contains("Setup panel reference is missing"));
        }

        [Test]
        public void GameplayLanePool_IsCreatedOnceUnderSceneNoteContainers()
        {
            Component root = CreateStaticHierarchy("Lane Pool UI");
            object gameplayHud = ReadProperty(root, "GameplayHud");
            object laneView = ReadProperty(gameplayHud, "RhythmLaneView");
            RectTransform guardContainer = (RectTransform)ReadProperty(laneView, "GuardNoteContainer");
            RectTransform strikeContainer = (RectTransform)ReadProperty(laneView, "StrikeNoteContainer");

            Invoke(laneView, "InitializeRuntimePool");
            int firstGuardCount = CountPooledNotes(guardContainer);
            int firstStrikeCount = CountPooledNotes(strikeContainer);
            Invoke(laneView, "InitializeRuntimePool");

            Assert.That(firstGuardCount, Is.EqualTo(24));
            Assert.That(firstStrikeCount, Is.EqualTo(24));
            Assert.That(CountPooledNotes(guardContainer), Is.EqualTo(24));
            Assert.That(CountPooledNotes(strikeContainer), Is.EqualTo(24));
            Assert.That(ReadProperty(laneView, "RuntimePoolCount"), Is.EqualTo(48));
        }

        [Test]
        public void BindButton_RebindingSameActionDoesNotDuplicateListener()
        {
            GameObject buttonObject = new GameObject("Binding Test Button", typeof(RectTransform), typeof(Image), typeof(Button));
            createdObjects.Add(buttonObject);
            Button button = buttonObject.GetComponent<Button>();
            int invocationCount = 0;
            UnityAction action = () => invocationCount++;
            Type factoryType = Type.GetType(FactoryTypeName, true);

            InvokeStatic(factoryType, "BindButton", button, action);
            InvokeStatic(factoryType, "BindButton", button, action);
            button.onClick.Invoke();

            Assert.That(invocationCount, Is.EqualTo(1));
        }

        private Component CreateStaticHierarchy(string name)
        {
            Type factoryType = Type.GetType(FactoryTypeName, true);
            Component root = (Component)InvokeStatic(factoryType, "CreateStaticHierarchy", null, name);
            createdObjects.Add(root.gameObject);
            return root;
        }

        private Component CreateComponent(string typeName, string objectName)
        {
            GameObject gameObject = new GameObject(objectName);
            createdObjects.Add(gameObject);
            return gameObject.AddComponent(Type.GetType(typeName, true));
        }

        private static int CountPooledNotes(Transform parent)
        {
            int count = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name.StartsWith("Pooled Note ", StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static object ReadProperty(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return property.GetValue(target);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = FindMethod(target.GetType(), methodName, arguments.Length, false);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(target, arguments);
        }

        private static object InvokeStatic(Type type, string methodName, params object[] arguments)
        {
            MethodInfo method = FindMethod(type, methodName, arguments.Length, true);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(null, arguments);
        }

        private static MethodInfo FindMethod(Type type, string methodName, int parameterCount, bool isStatic)
        {
            BindingFlags flags = BindingFlags.Public | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            MethodInfo[] methods = type.GetMethods(flags);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name == methodName && methods[i].GetParameters().Length == parameterCount)
                {
                    return methods[i];
                }
            }

            return null;
        }

        private static void DestroyNamedRoot(string name)
        {
            GameObject value = GameObject.Find(name);
            if (value != null)
            {
                UnityEngine.Object.DestroyImmediate(value);
            }
        }
    }
}
