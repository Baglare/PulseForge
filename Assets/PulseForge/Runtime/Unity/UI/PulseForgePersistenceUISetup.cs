using System;
using UnityEngine;

namespace PulseForge.Runtime.Unity.UI
{
    public static class PulseForgePersistenceUISetup
    {
        public static void Apply(
            PulseForgeSceneUIRoot root,
            Action<GameObject> registerCreated = null)
        {
            if (root == null)
            {
                return;
            }

            root.SetupPanel?.EnsurePersistenceControls(registerCreated);
            SavedTracksPanelView savedTracks = root.SavedTracksPanel;
            if (savedTracks == null && root.Canvas != null)
            {
                savedTracks = SavedTracksPanelView.Create(root.Canvas.transform);
                root.ConfigureSavedTracksPanel(savedTracks);
                registerCreated?.Invoke(savedTracks.PanelRoot);
            }

            savedTracks?.EnsureCacheStatusUI(registerCreated);
            savedTracks?.SetActive(false);
        }
    }
}
