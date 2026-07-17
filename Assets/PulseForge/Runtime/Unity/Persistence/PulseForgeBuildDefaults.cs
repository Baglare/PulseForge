using System;
using System.IO;
using UnityEngine;

namespace PulseForge.Runtime.Unity.Persistence
{
    [Serializable]
    internal sealed class PulseForgeBuildDefaultsData
    {
        public float beatmapOffsetMilliseconds;
        public float inputTimingOffsetMilliseconds;
    }

    public static class PulseForgeBuildDefaults
    {
        private const float MaximumOffsetMilliseconds = 500f;
        private static bool loaded;
        private static PulseForgeBuildDefaultsData cached;

        public static string RelativePath => "PulseForge/default-settings.json";

        public static void Apply(PulseForgeSettingsData settings)
        {
            if (settings == null) return;
            PulseForgeBuildDefaultsData data = Load();
            if (data == null) return;
            settings.beatmapOffsetSeconds = Mathf.Clamp(
                data.beatmapOffsetMilliseconds,
                -MaximumOffsetMilliseconds,
                MaximumOffsetMilliseconds) / 1000f;
            settings.inputTimingOffsetSeconds = Mathf.Clamp(
                data.inputTimingOffsetMilliseconds,
                -MaximumOffsetMilliseconds,
                MaximumOffsetMilliseconds) / 1000f;
        }

        internal static void ResetCacheForTests()
        {
            loaded = false;
            cached = null;
        }

        private static PulseForgeBuildDefaultsData Load()
        {
            if (loaded) return cached;
            loaded = true;
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, RelativePath);
                if (!File.Exists(path)) return null;
                string json = File.ReadAllText(path);
                cached = JsonUtility.FromJson<PulseForgeBuildDefaultsData>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("PulseForge build offset defaults could not be loaded: " + exception.Message);
                cached = null;
            }
            return cached;
        }
    }
}
