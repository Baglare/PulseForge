using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace PulseForge.Tests.EditMode
{
    public sealed class PulseForgeRuntimeFlowTests
    {
        private const string FlowTypeName = "PulseForge.Runtime.Unity.UI.PulseForgeRuntimeFlow, Assembly-CSharp";

        [Test]
        public void NewFlow_StartsInSetup()
        {
            object flow = CreateFlow();

            Assert.That(GetProperty(flow, "State").ToString(), Is.EqualTo("Setup"));
            Assert.That(GetProperty(flow, "ProcessingStage").ToString(), Is.EqualTo("None"));
        }

        [Test]
        public void SelectAudioPath_DoesNotStartPipeline()
        {
            object flow = CreateFlow();

            object selected = Invoke(flow, "SelectAudioPath", @"C:\Music\pulse.wav");

            Assert.That(selected, Is.EqualTo(true));
            Assert.That(GetProperty(flow, "State").ToString(), Is.EqualTo("Setup"));
            Assert.That(GetProperty(flow, "ProcessingStage").ToString(), Is.EqualTo("AudioSelected"));
            Assert.That(GetProperty(flow, "SelectedAudioPath"), Is.EqualTo(@"C:\Music\pulse.wav"));
        }

        [Test]
        public void SuccessfulFlow_TransitionsThroughReadyGameplayPauseAndCompleted()
        {
            object flow = CreateFlow();
            Invoke(flow, "SelectAudioPath", @"C:\Music\pulse.wav");

            Assert.That(Invoke(flow, "BeginProcessing"), Is.EqualTo(true));
            Assert.That(GetProperty(flow, "State").ToString(), Is.EqualTo("Processing"));

            Invoke(flow, "MarkReady");
            Assert.That(GetProperty(flow, "State").ToString(), Is.EqualTo("Ready"));

            Invoke(flow, "BeginSession", true);
            Assert.That(GetProperty(flow, "State").ToString(), Is.EqualTo("Countdown"));

            Invoke(flow, "MarkPlaying");
            Assert.That(GetProperty(flow, "State").ToString(), Is.EqualTo("Playing"));

            Invoke(flow, "Pause");
            Assert.That(GetProperty(flow, "State").ToString(), Is.EqualTo("Paused"));

            Invoke(flow, "Resume");
            Assert.That(GetProperty(flow, "State").ToString(), Is.EqualTo("Playing"));

            Invoke(flow, "Complete");
            Assert.That(GetProperty(flow, "State").ToString(), Is.EqualTo("Completed"));
        }

        [Test]
        public void ReturnToSetup_CanPreserveSelectedAudio()
        {
            object flow = CreateFlow();
            Invoke(flow, "SelectAudioPath", @"C:\Music\pulse.wav");
            Invoke(flow, "BeginProcessing");
            Invoke(flow, "MarkReady");

            Invoke(flow, "ReturnToSetup", false);

            Assert.That(GetProperty(flow, "State").ToString(), Is.EqualTo("Setup"));
            Assert.That(GetProperty(flow, "SelectedAudioPath"), Is.EqualTo(@"C:\Music\pulse.wav"));
            Assert.That(GetProperty(flow, "ProcessingStage").ToString(), Is.EqualTo("AudioSelected"));
        }

        [Test]
        public void FailureIsTerminalAndCannotBeOverwrittenByComplete()
        {
            object flow = CreateFlow();
            Invoke(flow, "MarkReady");
            Invoke(flow, "BeginSession", false);

            Invoke(flow, "Fail");
            Invoke(flow, "Complete");

            Assert.That(GetProperty(flow, "State").ToString(), Is.EqualTo("Failed"));
        }

        private static object CreateFlow()
        {
            Type flowType = Type.GetType(FlowTypeName, true);
            return Activator.CreateInstance(flowType);
        }

        private static object GetProperty(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            return property.GetValue(target);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = FindMethod(target.GetType(), methodName, arguments.Length);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(target, arguments);
        }

        private static MethodInfo FindMethod(Type type, string methodName, int parameterCount)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name == methodName && methods[i].GetParameters().Length == parameterCount)
                {
                    return methods[i];
                }
            }

            return null;
        }
    }

    public sealed class PulseForgeRuntimeControllerPresentationTests
    {
        private const string ControllerTypeName =
            "PulseForge.Runtime.Unity.Prototype.DebugRhythmPrototypeController, Assembly-CSharp";

        private GameObject controllerObject;

        [TearDown]
        public void TearDown()
        {
            GameObject uiRoot = GameObject.Find("PulseForge Runtime UI");
            if (uiRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(uiRoot);
            }

            if (controllerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(controllerObject);
            }
        }

        [Test]
        public void Start_PreparesSessionWithoutStartingIt()
        {
            Component controller = CreatePreparedController();

            Assert.That(ReadPublicProperty(controller, "UIState").ToString(), Is.EqualTo("Setup"));
            Assert.That(ReadPublicProperty(controller, "SessionEventCount"), Is.EqualTo(10));
            Assert.That(ReadPublicProperty(controller, "CanPause"), Is.EqualTo(false));
            Assert.That((double)ReadPublicProperty(controller, "CurrentSongTimeSeconds"), Is.EqualTo(0d));
        }

        [Test]
        public void ReturnToSetup_ChangesPresentationWithoutDiscardingSessionEvents()
        {
            Component controller = CreatePreparedController();
            int eventCount = (int)ReadPublicProperty(controller, "SessionEventCount");

            InvokePublicMethod(controller, "RestartSession");
            Assert.That(ReadPublicProperty(controller, "UIState").ToString(), Is.EqualTo("Countdown"));

            InvokePublicMethod(controller, "ChangeSettings");

            Assert.That(ReadPublicProperty(controller, "UIState").ToString(), Is.EqualTo("Setup"));
            Assert.That(ReadPublicProperty(controller, "SessionEventCount"), Is.EqualTo(eventCount));
        }

        private Component CreatePreparedController()
        {
            Type controllerType = Type.GetType(ControllerTypeName, true);
            controllerObject = new GameObject("PulseForge Controller Test");
            Component controller = controllerObject.AddComponent(controllerType);
            MethodInfo prepareSession = controllerType.GetMethod(
                "PrepareSession",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(prepareSession, Is.Not.Null);
            Assert.That(prepareSession.Invoke(controller, new object[] { false }), Is.EqualTo(true));
            return controller;
        }

        private static object ReadPublicProperty(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            return property.GetValue(target);
        }

        private static void InvokePublicMethod(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            method.Invoke(target, null);
        }

    }
}
