using System;
using UnityEngine;

namespace PulseForge.Runtime.Unity.UI
{
    public static class PulseForgeSettingsUISetup
    {
        public static void Apply(PulseForgeSceneUIRoot root, Action<GameObject> registerCreated = null)
        {
            if (root == null) return;
            root.SetupPanel?.EnsureSettingsButton(registerCreated);
            root.PauseOverlay?.EnsureSettingsButton(registerCreated);

            SettingsPanelView panel = root.SettingsPanel;
            if (panel == null && root.Canvas != null)
            {
                panel = SettingsPanelView.Create(root.Canvas.transform);
                registerCreated?.Invoke(panel.gameObject);
            }

            panel?.EnsureActionBindingControls(registerCreated);
            panel?.EnsureLanguageControl(registerCreated);
            panel?.EnsureM9HControls(registerCreated);
            panel?.EnsureScrollSensitivity();
            root.ConfigureSettingsPanel(panel);
            panel?.SetActive(false);
        }
    }
}
