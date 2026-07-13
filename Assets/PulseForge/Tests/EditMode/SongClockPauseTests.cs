using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace PulseForge.Tests.EditMode
{
    public sealed class SongClockPauseTests
    {
        [Test]
        public void RealtimeSongClock_PauseResume_UpdatesClockState()
        {
            object clock = CreateClock("PulseForge.Runtime.Unity.Timing.RealtimeSongClock");

            Invoke(clock, "Start");
            AssertClockState(clock, true, false);

            Invoke(clock, "Pause");
            AssertClockState(clock, false, true);

            Invoke(clock, "Resume");
            AssertClockState(clock, true, false);

            Invoke(clock, "Stop");
            AssertClockState(clock, false, false);
        }

        [Test]
        public void DspAudioSongClock_PauseResume_UpdatesClockAndAudioSourceState()
        {
            GameObject gameObject = new GameObject("DspAudioSongClockTest");
            AudioClip audioClip = AudioClip.Create("clock-test", 1000, 1, 1000, false);
            try
            {
                AudioSource audioSource = gameObject.AddComponent<AudioSource>();
                Type clockType = GetClockType("PulseForge.Runtime.Unity.Timing.DspAudioSongClock");
                object clock = Activator.CreateInstance(clockType, audioSource, audioClip, 0d);

                Invoke(clock, "Start");
                AssertClockState(clock, true, false);

                Invoke(clock, "Pause");
                AssertClockState(clock, false, true);

                Invoke(clock, "Resume");
                AssertClockState(clock, true, false);

                Invoke(clock, "Stop");
                AssertClockState(clock, false, false);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(audioClip);
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void DebugController_Start_PreparesSessionWithoutStartingClock()
        {
            GameObject gameObject = new GameObject("DebugControllerIdleTest");
            try
            {
                Type controllerType = GetClockType(
                    "PulseForge.Runtime.Unity.Prototype.DebugRhythmPrototypeController");
                Component controller = gameObject.AddComponent(controllerType);

                InvokeNonPublic(controller, "Start");

                object clock = GetNonPublicField(controller, "songClock");
                Assert.That(clock, Is.Not.Null);
                AssertClockState(clock, false, false);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void DebugController_ExplicitStartThenPauseResume_UpdatesClockState()
        {
            GameObject gameObject = new GameObject("DebugControllerPauseTest");
            try
            {
                Type controllerType = GetClockType(
                    "PulseForge.Runtime.Unity.Prototype.DebugRhythmPrototypeController");
                Component controller = gameObject.AddComponent(controllerType);
                SetNonPublicField(controller, "startCountdownSeconds", 0f);

                InvokeNonPublic(controller, "Start");
                Invoke(controller, "RestartSession");

                object clock = GetNonPublicField(controller, "songClock");
                AssertClockState(clock, true, false);

                InvokeNonPublic(controller, "TogglePause");
                AssertClockState(clock, false, true);

                InvokeNonPublic(controller, "TogglePause");
                AssertClockState(clock, true, false);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static object CreateClock(string fullTypeName)
        {
            return Activator.CreateInstance(GetClockType(fullTypeName));
        }

        private static Type GetClockType(string fullTypeName)
        {
            return Type.GetType(fullTypeName + ", Assembly-CSharp", true);
        }

        private static void Invoke(object clock, string methodName)
        {
            MethodInfo method = clock.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            method.Invoke(clock, null);
        }

        private static void InvokeNonPublic(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            method.Invoke(target, null);
        }

        private static object GetNonPublicField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(target);
        }

        private static void SetNonPublicField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static void AssertClockState(object clock, bool isRunning, bool isPaused)
        {
            Type clockType = clock.GetType();
            Assert.That((bool)clockType.GetProperty("IsRunning").GetValue(clock), Is.EqualTo(isRunning));
            Assert.That((bool)clockType.GetProperty("IsPaused").GetValue(clock), Is.EqualTo(isPaused));
        }
    }
}
