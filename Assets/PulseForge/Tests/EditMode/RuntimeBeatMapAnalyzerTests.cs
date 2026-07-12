using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PulseForge.Domain.Rhythm;
using UnityEngine;

namespace PulseForge.Tests.EditMode
{
    public sealed class RuntimeBeatMapAnalyzerTests
    {
        [Test]
        public void BuildBeatEvents_ThreeSeparatedClicks_UsesDefaultLegacyPattern()
        {
            AudioClip audioClip = CreateClickClip(0.20f, 0.50f, 0.80f);
            try
            {
                IReadOnlyList<BeatEventData> events = InvokeBuildBeatEvents(audioClip);

                Assert.That(events.Count, Is.EqualTo(3));
                Assert.That(events[0].Action, Is.EqualTo(RhythmAction.Guard));
                Assert.That(events[1].Action, Is.EqualTo(RhythmAction.Guard));
                Assert.That(events[2].Action, Is.EqualTo(RhythmAction.Strike));
                Assert.That(events[0].TargetTimeSeconds, Is.EqualTo(0.19d).Within(0.03d));
                Assert.That(events[1].TargetTimeSeconds, Is.EqualTo(0.49d).Within(0.03d));
                Assert.That(events[2].TargetTimeSeconds, Is.EqualTo(0.79d).Within(0.03d));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(audioClip);
            }
        }

        [Test]
        public void BuildBeatEvents_RuntimePipelineSettings_ApplyDifficultyAndDefensiveStyle()
        {
            AudioClip audioClip = CreateClickClip(0.20f, 0.50f, 0.80f);
            try
            {
                IReadOnlyList<BeatEventData> easyEvents = InvokeBuildBeatEvents(
                    audioClip,
                    "Onset",
                    "Easy",
                    "Defensive");
                IReadOnlyList<BeatEventData> hardEvents = InvokeBuildBeatEvents(
                    audioClip,
                    "Onset",
                    "Hard",
                    "Defensive");

                Assert.That(easyEvents.Count, Is.EqualTo(2));
                Assert.That(hardEvents.Count, Is.EqualTo(3));
                Assert.That(hardEvents[0].Action, Is.EqualTo(RhythmAction.Guard));
                Assert.That(hardEvents[1].Action, Is.EqualTo(RhythmAction.Guard));
                Assert.That(hardEvents[2].Action, Is.EqualTo(RhythmAction.Strike));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(audioClip);
            }
        }

        [Test]
        public void BuildBeatEvents_SilentClip_ReportsNoPlayableBeats()
        {
            AudioClip audioClip = AudioClip.Create("silent", 1000, 1, 1000, false);
            try
            {
                TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                    () => InvokeBuildBeatEvents(audioClip));

                Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
                Assert.That(exception.InnerException.Message, Does.Contain("No playable beats"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(audioClip);
            }
        }

        private static AudioClip CreateClickClip(params float[] clickTimesSeconds)
        {
            const int sampleRate = 1000;
            float[] samples = new float[sampleRate];
            for (int i = 0; i < clickTimesSeconds.Length; i++)
            {
                int sampleIndex = Mathf.RoundToInt(clickTimesSeconds[i] * sampleRate);
                samples[sampleIndex] = 1f;
            }

            AudioClip audioClip = AudioClip.Create("clicks", samples.Length, 1, sampleRate, false);
            Assert.That(audioClip.SetData(samples, 0), Is.True);
            return audioClip;
        }

        private static IReadOnlyList<BeatEventData> InvokeBuildBeatEvents(AudioClip audioClip)
        {
            Type analyzerType = Type.GetType(
                "PulseForge.Runtime.Unity.Audio.RuntimeBeatMapAnalyzer, Assembly-CSharp",
                true);
            MethodInfo buildMethod = FindBuildMethod(analyzerType, 1);
            Assert.That(buildMethod, Is.Not.Null);
            return (IReadOnlyList<BeatEventData>)buildMethod.Invoke(null, new object[] { audioClip });
        }

        private static IReadOnlyList<BeatEventData> InvokeBuildBeatEvents(
            AudioClip audioClip,
            string detectionMode,
            string difficulty,
            string combatStyle)
        {
            Type analyzerType = Type.GetType(
                "PulseForge.Runtime.Unity.Audio.RuntimeBeatMapAnalyzer, Assembly-CSharp",
                true);
            Type settingsType = Type.GetType(
                "PulseForge.Runtime.Unity.Audio.RuntimeAudioPipelineSettings, Assembly-CSharp",
                true);
            Type detectionType = Type.GetType(
                "PulseForge.Runtime.Unity.Audio.RuntimeDetectionMode, Assembly-CSharp",
                true);
            Type difficultyType = Type.GetType(
                "PulseForge.Runtime.Unity.Audio.RuntimeDifficulty, Assembly-CSharp",
                true);
            Type combatStyleType = Type.GetType(
                "PulseForge.Runtime.Unity.Audio.RuntimeCombatStyle, Assembly-CSharp",
                true);
            object settings = Activator.CreateInstance(
                settingsType,
                Enum.Parse(detectionType, detectionMode),
                Enum.Parse(difficultyType, difficulty),
                Enum.Parse(combatStyleType, combatStyle));
            MethodInfo buildMethod = FindBuildMethod(analyzerType, 2);
            Assert.That(buildMethod, Is.Not.Null);
            return (IReadOnlyList<BeatEventData>)buildMethod.Invoke(null, new[] { audioClip, settings });
        }

        private static MethodInfo FindBuildMethod(Type analyzerType, int parameterCount)
        {
            MethodInfo[] methods = analyzerType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name == "BuildBeatEvents"
                    && methods[i].GetParameters().Length == parameterCount)
                {
                    return methods[i];
                }
            }

            return null;
        }
    }
}
