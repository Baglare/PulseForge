using System;
using System.Collections.Generic;
using UnityEngine;

namespace PulseForge.Runtime.Unity.Persistence
{
    public readonly struct PulseForgeResolutionOption
    {
        public PulseForgeResolutionOption(int width, int height, int refreshRate)
        {
            Width = width;
            Height = height;
            RefreshRate = refreshRate;
        }

        public int Width { get; }
        public int Height { get; }
        public int RefreshRate { get; }
        public string Label => Width + " × " + Height + " @ " + RefreshRate + " Hz";
    }

    public static class PulseForgeRuntimeSettingsApplier
    {
        public static void ApplyAudio(PulseForgeSettingsData settings, AudioSource musicSource)
        {
            PulseForgeSettingsData normalized = SaveDataNormalizer.NormalizeSettings(settings);
            AudioListener.volume = normalized.audio.masterVolume;
            if (musicSource != null)
            {
                musicSource.volume = normalized.audio.musicVolume;
            }
        }

        public static void ApplyDisplay(PulseForgeSettingsData settings)
        {
            PulseForgeSettingsData normalized = SaveDataNormalizer.NormalizeSettings(settings);
            PulseForgeDisplaySettingsData display = normalized.display;
            FullScreenMode mode = ResolveFullScreenMode(display.displayMode);
            Screen.SetResolution(
                display.resolutionWidth,
                display.resolutionHeight,
                mode,
                display.refreshRate);
            QualitySettings.vSyncCount = display.vSync ? 1 : 0;
            Application.targetFrameRate = display.vSync ? -1 : display.frameRateLimit;
        }

        public static IReadOnlyList<PulseForgeResolutionOption> GetAvailableResolutions()
        {
            List<PulseForgeResolutionOption> options = new List<PulseForgeResolutionOption>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            Resolution[] resolutions = Screen.resolutions;
            for (int i = 0; i < resolutions.Length; i++)
            {
                Resolution resolution = resolutions[i];
                int refreshRate = Math.Max(1, (int)Math.Round(resolution.refreshRateRatio.value));
                string key = resolution.width + "x" + resolution.height + "@" + refreshRate;
                if (seen.Add(key))
                {
                    options.Add(new PulseForgeResolutionOption(
                        resolution.width,
                        resolution.height,
                        refreshRate));
                }
            }

            if (options.Count == 0)
            {
                Resolution current = Screen.currentResolution;
                options.Add(new PulseForgeResolutionOption(
                    Math.Max(640, current.width),
                    Math.Max(360, current.height),
                    Math.Max(1, (int)Math.Round(current.refreshRateRatio.value))));
            }

            return options;
        }

        public static FullScreenMode ResolveFullScreenMode(string value)
        {
            if (!Enum.TryParse(value, true, out PulseForgeDisplayMode mode))
            {
                mode = PulseForgeDisplayMode.Windowed;
            }

            switch (mode)
            {
                case PulseForgeDisplayMode.Borderless:
                    return FullScreenMode.FullScreenWindow;
                case PulseForgeDisplayMode.ExclusiveFullscreen:
                    return FullScreenMode.ExclusiveFullScreen;
                default:
                    return FullScreenMode.Windowed;
            }
        }
    }
}
