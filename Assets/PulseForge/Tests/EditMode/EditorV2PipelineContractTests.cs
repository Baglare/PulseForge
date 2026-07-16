using System;
using System.Reflection;
using NUnit.Framework;

namespace PulseForge.Tests.EditMode
{
    public sealed class EditorV2PipelineContractTests
    {
        [Test]
        public void LegacyPythonV1PipelineEntryPointIsPreserved()
        {
            Type window = Type.GetType(
                "PulseForge.Editor.AudioPipeline.PulseForgeAudioPipelineWindow, Assembly-CSharp-Editor",
                true);
            Type pipelineMode = window.GetNestedType("PipelineMode", BindingFlags.NonPublic);
            MethodInfo legacyRun = window.GetMethod(
                "RunLegacyPythonV1Pipeline",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(pipelineMode, Is.Not.Null);
            Assert.That(Enum.GetNames(pipelineMode), Does.Contain("RadialV2"));
            Assert.That(Enum.GetNames(pipelineMode), Does.Contain("LegacyPythonV1"));
            Assert.That(legacyRun, Is.Not.Null);
        }
    }
}
