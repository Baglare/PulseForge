using System;
using System.IO;
using System.Text;
using PulseForge.Runtime.Unity.Persistence;
using UnityEditor;
using UnityEngine;

namespace PulseForge.Editor.UI
{
    internal static class PulseForgeBuildDefaultsEditor
    {
        private const string CaptureMenu =
            "Tools/PulseForge/Settings/Capture Current Offsets as Build Defaults";

        [Serializable]
        private sealed class OffsetDefaults
        {
            public float beatmapOffsetMilliseconds;
            public float inputTimingOffsetMilliseconds;
        }

        [MenuItem(CaptureMenu)]
        private static void CaptureCurrentOffsets()
        {
            PulseForgeSaveService service = new PulseForgeSaveService();
            service.Initialize();
            PulseForgeSettingsData settings = SaveDataNormalizer.NormalizeSettings(service.Settings);
            OffsetDefaults data = new OffsetDefaults
            {
                beatmapOffsetMilliseconds = settings.beatmapOffsetSeconds * 1000f,
                inputTimingOffsetMilliseconds = settings.inputTimingOffsetSeconds * 1000f
            };

            string path = Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                PulseForgeBuildDefaults.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonUtility.ToJson(data, true) + Environment.NewLine,
                new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log(
                "PulseForge build offset defaults captured: beatmap "
                + data.beatmapOffsetMilliseconds.ToString("0.###")
                + " ms, input "
                + data.inputTimingOffsetMilliseconds.ToString("0.###")
                + " ms.");
        }
    }
}
