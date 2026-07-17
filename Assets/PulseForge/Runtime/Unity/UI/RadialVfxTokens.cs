using System;
using System.Collections.Generic;
using PulseForge.AudioAnalysis;
using UnityEngine;

namespace PulseForge.Runtime.Unity.UI
{
    public static class RadialVfxTokens
    {
        public const int PoolCapacity = 28;

        public const float GuardEffectScale = 1.00f;
        public const float LightEffectScale = 0.82f;
        public const float HeavyEffectScale = 1.24f;
        public const float DodgeEffectScale = 0.92f;
        public const float PerfectMultiplier = 1.18f;
        public const float GoodMultiplier = 0.88f;
        public const float MissReaction = 0.78f;

        public const float BeatPulseIntensity = 0.22f;
        public const float DownbeatPulseIntensity = 0.38f;
        public const float SubdivisionPulseIntensity = 0.10f;
        public const float QuietSectionMultiplier = 0.58f;
        public const float ActiveSectionMultiplier = 1.00f;
        public const float PeakSectionMultiplier = 1.16f;
        public const float HighClarityVfxMultiplier = 0.52f;

        public const float FogTransitionDuration = 0.28f;
        public const float CombatEffectDuration = 0.34f;
        public const float HeavyEffectDuration = 0.46f;
        public const float BreakSegmentDuration = 0.30f;
        public const float ProjectileReactionDuration = 0.36f;
    }

    public readonly struct RadialReactiveVisual
    {
        public RadialReactiveVisual(
            float beat,
            float downbeat,
            float subdivision,
            float core,
            float direction,
            float ambient,
            float activityMultiplier)
        {
            Beat = Mathf.Clamp01(beat);
            Downbeat = Mathf.Clamp01(downbeat);
            Subdivision = Mathf.Clamp01(subdivision);
            Core = Mathf.Clamp01(core);
            Direction = Mathf.Clamp01(direction);
            Ambient = Mathf.Clamp01(ambient);
            ActivityMultiplier = Mathf.Clamp(activityMultiplier, 0f, 1.25f);
        }

        public float Beat { get; }
        public float Downbeat { get; }
        public float Subdivision { get; }
        public float Core { get; }
        public float Direction { get; }
        public float Ambient { get; }
        public float ActivityMultiplier { get; }
    }

    public static class RadialReactivePresentationMath
    {
        public static RadialReactiveVisual Evaluate(
            BeatGridData beatGrid,
            double songTimeSeconds,
            float eventIntensity,
            bool pulseEnabled,
            bool highClarity)
        {
            float intensity = Mathf.Clamp01(eventIntensity);
            float activity = ResolveActivityMultiplier(intensity);
            float clarity = highClarity
                ? RadialVfxTokens.HighClarityVfxMultiplier
                : 1f;
            if (!pulseEnabled || beatGrid == null)
            {
                return new RadialReactiveVisual(
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    Mathf.Clamp01((activity - 0.52f) * 0.24f * clarity),
                    activity);
            }

            RadialBeatPulseVisual pulse = RadialPresentationMath.EvaluateBeatPulse(
                beatGrid,
                songTimeSeconds);
            float beat = pulse.IsSubdivision ? 0f : pulse.Strength;
            float subdivision = pulse.IsSubdivision ? pulse.Strength : 0f;
            float downbeat = IsDownbeat(beatGrid.beatTimesSeconds, songTimeSeconds)
                ? beat
                : 0f;
            float combined = Mathf.Clamp01(
                beat * RadialVfxTokens.BeatPulseIntensity
                + downbeat * RadialVfxTokens.DownbeatPulseIntensity
                + subdivision * RadialVfxTokens.SubdivisionPulseIntensity);
            combined *= activity * clarity;

            return new RadialReactiveVisual(
                beat * RadialVfxTokens.BeatPulseIntensity * activity * clarity,
                downbeat * RadialVfxTokens.DownbeatPulseIntensity * activity * clarity,
                subdivision * RadialVfxTokens.SubdivisionPulseIntensity * activity * clarity,
                combined,
                Mathf.Clamp01(combined * 1.12f),
                Mathf.Clamp01((activity - 0.52f) * 0.24f * clarity),
                activity);
        }

        private static float ResolveActivityMultiplier(float intensity)
        {
            if (intensity < 0.38f)
            {
                return Mathf.Lerp(
                    RadialVfxTokens.QuietSectionMultiplier,
                    RadialVfxTokens.ActiveSectionMultiplier,
                    intensity / 0.38f);
            }

            return Mathf.Lerp(
                RadialVfxTokens.ActiveSectionMultiplier,
                RadialVfxTokens.PeakSectionMultiplier,
                Mathf.InverseLerp(0.38f, 1f, intensity));
        }

        private static bool IsDownbeat(IList<double> beats, double songTimeSeconds)
        {
            if (beats == null || beats.Count == 0)
            {
                return false;
            }

            int index = FindNearestIndex(beats, songTimeSeconds);
            return index >= 0
                && index % 4 == 0
                && Math.Abs(beats[index] - songTimeSeconds) <= 0.16d;
        }

        private static int FindNearestIndex(IList<double> values, double target)
        {
            int low = 0;
            int high = values.Count - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                double value = values[middle];
                if (value < target)
                {
                    low = middle + 1;
                }
                else if (value > target)
                {
                    high = middle - 1;
                }
                else
                {
                    return middle;
                }
            }

            if (low >= values.Count)
            {
                return values.Count - 1;
            }
            if (high < 0)
            {
                return 0;
            }
            return Math.Abs(values[low] - target) < Math.Abs(values[high] - target)
                ? low
                : high;
        }
    }
}
