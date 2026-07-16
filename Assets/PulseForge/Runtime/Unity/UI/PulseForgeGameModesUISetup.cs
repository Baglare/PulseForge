using System;
using UnityEngine;

namespace PulseForge.Runtime.Unity.UI
{
    public static class PulseForgeGameModesUISetup
    {
        public static void Apply(
            PulseForgeSceneUIRoot root,
            Action<GameObject> registerCreated = null)
        {
            if (root == null) return;
            root.SetupPanel?.EnsureGameModeControls(registerCreated);
            root.ReadyPanel?.EnsureGameModeControls(registerCreated);
            root.GameplayHud?.EnsureGameModeHud(registerCreated);
            root.ResultsPanel?.EnsureOutcomeFields(registerCreated);
            root.SettingsPanel?.EnsureGameModeControl(registerCreated);
        }
    }

    public static class PulseForgeCoverageUISetup
    {
        public static void Apply(
            PulseForgeSceneUIRoot root,
            Action<GameObject> registerCreated = null)
        {
            if (root == null) return;
            root.SetupPanel?.EnsureCoverageControls(registerCreated);
            root.ReadyPanel?.EnsureCoverageControls(registerCreated);
            root.SettingsPanel?.EnsureCoverageControl(registerCreated);
        }
    }

    public static class PulseForgePlayabilityAssistUISetup
    {
        public static void Apply(
            PulseForgeSceneUIRoot root,
            Action<GameObject> registerCreated = null)
        {
            if (root == null) return;
            root.SetupPanel?.EnsureTimingAssistControls(registerCreated);
            root.ReadyPanel?.EnsureTimingAssistControls(registerCreated);
            root.SettingsPanel?.EnsurePlayabilityAssistControls(registerCreated);
            root.GameplayHud?.EnsurePlayabilityAssistHud(registerCreated);
        }
    }
}
