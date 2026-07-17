using System;
using System.Collections.Generic;
using UnityEngine;

namespace PulseForge.Runtime.Unity.Onboarding
{
    internal sealed class DspClickTrack : IDisposable
    {
        private const int SourceCount = 4;
        private const double ScheduleAheadSeconds = 1.5d;

        private readonly GameObject root;
        private readonly AudioSource[] sources;
        private readonly AudioClip clickClip;
        private readonly List<double> finiteTargets = new List<double>();
        private int sourceIndex;
        private int finiteIndex;
        private bool looping;
        private double loopIntervalSeconds;
        private double nextLoopDspTime;

        public DspClickTrack(GameObject host)
        {
            root = new GameObject("PulseForge M9H DSP Click Track");
            root.hideFlags = HideFlags.DontSave;
            root.transform.SetParent(host == null ? null : host.transform, false);
            clickClip = CreateClickClip();
            sources = new AudioSource[SourceCount];
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i] = root.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].loop = false;
                sources[i].spatialBlend = 0f;
                sources[i].volume = 0.65f;
                sources[i].clip = clickClip;
            }
        }

        public void StartLoop(double startDspTime, double intervalSeconds)
        {
            Stop();
            looping = true;
            loopIntervalSeconds = Math.Max(0.25d, intervalSeconds);
            nextLoopDspTime = Math.Max(AudioSettings.dspTime + 0.05d, startDspTime);
        }

        public void StartFinite(IReadOnlyList<double> targetDspTimes)
        {
            Stop();
            if (targetDspTimes == null)
            {
                return;
            }
            for (int i = 0; i < targetDspTimes.Count; i++)
            {
                if (targetDspTimes[i] > AudioSettings.dspTime)
                {
                    finiteTargets.Add(targetDspTimes[i]);
                }
            }
        }

        public void Update()
        {
            double scheduleLimit = AudioSettings.dspTime + ScheduleAheadSeconds;
            if (looping)
            {
                while (nextLoopDspTime <= scheduleLimit)
                {
                    Schedule(nextLoopDspTime);
                    nextLoopDspTime += loopIntervalSeconds;
                }
                return;
            }
            while (finiteIndex < finiteTargets.Count
                && finiteTargets[finiteIndex] <= scheduleLimit)
            {
                Schedule(finiteTargets[finiteIndex]);
                finiteIndex++;
            }
        }

        public float GetVisualPulse(double visualOffsetSeconds)
        {
            double now = AudioSettings.dspTime;
            double nearest = double.MaxValue;
            if (looping && loopIntervalSeconds > 0d)
            {
                double previous = nextLoopDspTime - loopIntervalSeconds;
                double steps = Math.Round((now - previous) / loopIntervalSeconds);
                nearest = previous + (steps * loopIntervalSeconds) + visualOffsetSeconds;
            }
            else
            {
                for (int i = 0; i < finiteTargets.Count; i++)
                {
                    double candidate = finiteTargets[i] + visualOffsetSeconds;
                    if (Math.Abs(candidate - now) < Math.Abs(nearest - now))
                    {
                        nearest = candidate;
                    }
                }
            }
            if (nearest == double.MaxValue)
            {
                return 0f;
            }
            double distance = Math.Abs(nearest - now);
            return distance >= 0.16d ? 0f : (float)(1d - (distance / 0.16d));
        }

        public void Stop()
        {
            looping = false;
            finiteTargets.Clear();
            finiteIndex = 0;
            sourceIndex = 0;
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i].Stop();
            }
        }

        public void Dispose()
        {
            Stop();
            if (clickClip != null)
            {
                UnityEngine.Object.Destroy(clickClip);
            }
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        private void Schedule(double dspTime)
        {
            AudioSource source = sources[sourceIndex];
            sourceIndex = (sourceIndex + 1) % sources.Length;
            source.clip = clickClip;
            source.PlayScheduled(dspTime);
        }

        private static AudioClip CreateClickClip()
        {
            int sampleRate = AudioSettings.outputSampleRate > 0
                ? AudioSettings.outputSampleRate
                : 48000;
            int sampleCount = Math.Max(1, (int)(sampleRate * 0.035f));
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float time = (float)i / sampleRate;
                float envelope = Mathf.Exp(-time * 85f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * 1500f * time) * envelope * 0.75f;
            }
            AudioClip clip = AudioClip.Create(
                "PulseForge Calibration Click",
                sampleCount,
                1,
                sampleRate,
                false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
