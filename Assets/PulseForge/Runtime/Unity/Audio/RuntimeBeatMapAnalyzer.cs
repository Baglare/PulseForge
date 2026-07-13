using System;
using System.Collections.Generic;
using PulseForge.Domain.Rhythm;
using UnityEngine;

namespace PulseForge.Runtime.Unity.Audio
{
    internal static class RuntimeBeatMapAnalyzer
    {
        private const double FrameMilliseconds = 10d;
        private const double BaselineMilliseconds = 120d;
        private const double ThresholdRatio = 0.35d;
        private const double IntensityStrikeThreshold = 0.65d;
        private const double BurstWindowSeconds = 0.35d;
        private const int OnsetSmoothRadius = 1;
        private const int MaximumEventCount = 512;
        private static readonly RhythmAction[] LegacyPattern =
        {
            RhythmAction.Guard,
            RhythmAction.Guard,
            RhythmAction.Strike,
            RhythmAction.Guard,
            RhythmAction.Strike,
            RhythmAction.Strike,
            RhythmAction.Guard,
            RhythmAction.Strike,
            RhythmAction.Guard,
            RhythmAction.Strike
        };
        private static readonly RhythmAction[] BalancedPattern =
        {
            RhythmAction.Guard,
            RhythmAction.Guard,
            RhythmAction.Strike,
            RhythmAction.Guard,
            RhythmAction.Strike,
            RhythmAction.Strike
        };
        private static readonly RhythmAction[] AggressivePattern =
        {
            RhythmAction.Strike,
            RhythmAction.Strike,
            RhythmAction.Guard,
            RhythmAction.Strike
        };

        public static IReadOnlyList<BeatEventData> BuildBeatEvents(AudioClip audioClip)
        {
            return BuildBeatEvents(audioClip, RuntimeAudioPipelineSettings.Default);
        }

        public static IReadOnlyList<BeatEventData> BuildBeatEvents(
            AudioClip audioClip,
            RuntimeAudioPipelineSettings settings)
        {
            if (audioClip == null)
            {
                throw new ArgumentNullException(nameof(audioClip));
            }

            if (audioClip.frequency <= 0 || audioClip.channels <= 0 || audioClip.samples <= 0)
            {
                throw new InvalidOperationException("The converted audio clip contains no readable samples.");
            }

            List<AnalysisFrame> amplitudeFrames = ReadPeakAmplitudeFrames(audioClip);
            List<double> detectionValues = settings.DetectionMode == RuntimeDetectionMode.Amplitude
                ? BuildAmplitudeCurve(amplitudeFrames)
                : BuildSmoothedOnsetCurve(amplitudeFrames);
            List<DetectedPeak> peaks = LimitPeakCount(
                DetectPeaks(amplitudeFrames, detectionValues, ResolveMinimumGapSeconds(settings.Difficulty)));

            if (peaks.Count == 0)
            {
                throw new InvalidOperationException("No playable beats were detected in this audio file.");
            }

            BeatEventData[] beatEvents = new BeatEventData[peaks.Count];
            RhythmAction? previousAction = null;
            for (int i = 0; i < peaks.Count; i++)
            {
                RhythmAction action = SelectAction(i, peaks, previousAction, settings.CombatStyle);
                beatEvents[i] = new BeatEventData(
                    "runtime-" + (i + 1).ToString("D3"),
                    peaks[i].TimeSeconds,
                    action,
                    (float)Math.Max(0d, Math.Min(1d, peaks[i].Intensity)));
                previousAction = action;
            }

            return beatEvents;
        }

        private static List<double> BuildAmplitudeCurve(IReadOnlyList<AnalysisFrame> frames)
        {
            List<double> amplitudes = new List<double>(frames.Count);
            for (int i = 0; i < frames.Count; i++)
            {
                amplitudes.Add(frames[i].Amplitude);
            }

            return amplitudes;
        }

        private static List<AnalysisFrame> ReadPeakAmplitudeFrames(AudioClip audioClip)
        {
            int channels = audioClip.channels;
            int sampleRate = audioClip.frequency;
            int samplesPerAnalysisFrame = Math.Max(1, (int)Math.Round(sampleRate * FrameMilliseconds / 1000d));
            int chunkFrameCount = Math.Max(samplesPerAnalysisFrame, sampleRate);
            List<AnalysisFrame> frames = new List<AnalysisFrame>();

            double framePeak = 0d;
            int samplesInAnalysisFrame = 0;
            int analysisFrameIndex = 0;

            for (int offsetFrame = 0; offsetFrame < audioClip.samples; offsetFrame += chunkFrameCount)
            {
                int readableFrameCount = Math.Min(chunkFrameCount, audioClip.samples - offsetFrame);
                float[] sampleBuffer = new float[readableFrameCount * channels];
                if (!audioClip.GetData(sampleBuffer, offsetFrame))
                {
                    throw new InvalidOperationException("Unity could not read the converted WAV sample data.");
                }

                for (int frameIndex = 0; frameIndex < readableFrameCount; frameIndex++)
                {
                    double amplitudeTotal = 0d;
                    int sampleStart = frameIndex * channels;
                    for (int channelIndex = 0; channelIndex < channels; channelIndex++)
                    {
                        amplitudeTotal += Math.Abs(sampleBuffer[sampleStart + channelIndex]);
                    }

                    double amplitude = amplitudeTotal / channels;
                    if (amplitude > framePeak)
                    {
                        framePeak = amplitude;
                    }

                    samplesInAnalysisFrame++;
                    if (samplesInAnalysisFrame >= samplesPerAnalysisFrame)
                    {
                        frames.Add(new AnalysisFrame(
                            analysisFrameIndex * samplesPerAnalysisFrame / (double)sampleRate,
                            framePeak));
                        analysisFrameIndex++;
                        framePeak = 0d;
                        samplesInAnalysisFrame = 0;
                    }
                }
            }

            if (samplesInAnalysisFrame > 0)
            {
                frames.Add(new AnalysisFrame(
                    analysisFrameIndex * samplesPerAnalysisFrame / (double)sampleRate,
                    framePeak));
            }

            return frames;
        }

        private static List<double> BuildSmoothedOnsetCurve(IReadOnlyList<AnalysisFrame> frames)
        {
            int baselineFrameCount = Math.Max(1, (int)Math.Round(BaselineMilliseconds / FrameMilliseconds));
            double[] prefixSums = new double[frames.Count + 1];
            double[] onsetValues = new double[frames.Count];

            for (int i = 0; i < frames.Count; i++)
            {
                prefixSums[i + 1] = prefixSums[i] + frames[i].Amplitude;
                int baselineStartIndex = Math.Max(0, i - baselineFrameCount);
                int baselineSampleCount = i - baselineStartIndex;
                double baseline = baselineSampleCount == 0
                    ? 0d
                    : (prefixSums[i] - prefixSums[baselineStartIndex]) / baselineSampleCount;
                onsetValues[i] = Math.Max(0d, frames[i].Amplitude - baseline);
            }

            List<double> smoothedValues = new List<double>(frames.Count);
            for (int i = 0; i < onsetValues.Length; i++)
            {
                int startIndex = Math.Max(0, i - OnsetSmoothRadius);
                int endIndex = Math.Min(onsetValues.Length - 1, i + OnsetSmoothRadius);
                double total = 0d;
                for (int valueIndex = startIndex; valueIndex <= endIndex; valueIndex++)
                {
                    total += onsetValues[valueIndex];
                }

                smoothedValues.Add(total / (endIndex - startIndex + 1));
            }

            return smoothedValues;
        }

        private static List<DetectedPeak> DetectPeaks(
            IReadOnlyList<AnalysisFrame> frames,
            IReadOnlyList<double> detectionValues,
            double minimumGapSeconds)
        {
            List<DetectedPeak> peaks = new List<DetectedPeak>();
            if (detectionValues.Count == 0)
            {
                return peaks;
            }

            double maximumDetectionValue = 0d;
            for (int i = 0; i < detectionValues.Count; i++)
            {
                maximumDetectionValue = Math.Max(maximumDetectionValue, detectionValues[i]);
            }

            if (maximumDetectionValue <= 0d)
            {
                return peaks;
            }

            double threshold = maximumDetectionValue * ThresholdRatio;
            for (int i = 0; i < detectionValues.Count; i++)
            {
                double value = detectionValues[i];
                if (value <= 0d || value < threshold)
                {
                    continue;
                }

                double previousValue = i > 0 ? detectionValues[i - 1] : -1d;
                double nextValue = i < detectionValues.Count - 1 ? detectionValues[i + 1] : -1d;
                if (value < previousValue || value < nextValue || (value <= previousValue && value <= nextValue))
                {
                    continue;
                }

                DetectedPeak peak = new DetectedPeak(frames[i].TimeSeconds, value / maximumDetectionValue);
                if (peaks.Count > 0 && peak.TimeSeconds - peaks[peaks.Count - 1].TimeSeconds < minimumGapSeconds)
                {
                    if (peak.Intensity > peaks[peaks.Count - 1].Intensity)
                    {
                        peaks[peaks.Count - 1] = peak;
                    }

                    continue;
                }

                peaks.Add(peak);
            }

            return peaks;
        }

        private static double ResolveMinimumGapSeconds(RuntimeDifficulty difficulty)
        {
            switch (difficulty)
            {
                case RuntimeDifficulty.Easy:
                    return 0.45d;
                case RuntimeDifficulty.Hard:
                    return 0.18d;
                default:
                    return 0.28d;
            }
        }

        private static RhythmAction SelectAction(
            int index,
            IReadOnlyList<DetectedPeak> peaks,
            RhythmAction? previousAction,
            RuntimeCombatStyle combatStyle)
        {
            DetectedPeak peak = peaks[index];
            switch (combatStyle)
            {
                case RuntimeCombatStyle.Balanced:
                {
                    RhythmAction action = BalancedPattern[index % BalancedPattern.Length];
                    if (peak.Intensity >= IntensityStrikeThreshold
                        && action == RhythmAction.Guard
                        && index % 3 == 1
                        && previousAction != RhythmAction.Strike)
                    {
                        return RhythmAction.Strike;
                    }

                    return action;
                }
                case RuntimeCombatStyle.Defensive:
                    if (peak.Intensity >= IntensityStrikeThreshold
                        && index % 3 == 2
                        && previousAction != RhythmAction.Strike)
                    {
                        return RhythmAction.Strike;
                    }

                    return RhythmAction.Guard;
                case RuntimeCombatStyle.Aggressive:
                    if (peak.Intensity < IntensityStrikeThreshold && index % 4 == 1)
                    {
                        return RhythmAction.Guard;
                    }

                    return AggressivePattern[index % AggressivePattern.Length];
                case RuntimeCombatStyle.Bursty:
                    if (peak.Intensity >= IntensityStrikeThreshold)
                    {
                        return RhythmAction.Strike;
                    }

                    if (index > 0 && peak.TimeSeconds - peaks[index - 1].TimeSeconds <= BurstWindowSeconds)
                    {
                        return RhythmAction.Strike;
                    }

                    return BalancedPattern[index % BalancedPattern.Length];
                default:
                    return LegacyPattern[index % LegacyPattern.Length];
            }
        }

        private static List<DetectedPeak> LimitPeakCount(List<DetectedPeak> peaks)
        {
            if (peaks.Count <= MaximumEventCount)
            {
                return peaks;
            }

            List<DetectedPeak> limitedPeaks = new List<DetectedPeak>(MaximumEventCount);
            for (int i = 0; i < MaximumEventCount; i++)
            {
                int sourceIndex = (int)Math.Round(i * (peaks.Count - 1d) / (MaximumEventCount - 1d));
                limitedPeaks.Add(peaks[sourceIndex]);
            }

            return limitedPeaks;
        }

        private readonly struct AnalysisFrame
        {
            public AnalysisFrame(double timeSeconds, double amplitude)
            {
                TimeSeconds = timeSeconds;
                Amplitude = amplitude;
            }

            public double TimeSeconds { get; }

            public double Amplitude { get; }
        }

        private readonly struct DetectedPeak
        {
            public DetectedPeak(double timeSeconds, double intensity)
            {
                TimeSeconds = timeSeconds;
                Intensity = intensity;
            }

            public double TimeSeconds { get; }

            public double Intensity { get; }
        }
    }

    public readonly struct RuntimeAudioPipelineSettings
    {
        public RuntimeAudioPipelineSettings(
            RuntimeDetectionMode detectionMode,
            RuntimeDifficulty difficulty,
            RuntimeCombatStyle combatStyle)
        {
            DetectionMode = detectionMode;
            Difficulty = difficulty;
            CombatStyle = combatStyle;
        }

        public static RuntimeAudioPipelineSettings Default
        {
            get
            {
                return new RuntimeAudioPipelineSettings(
                    RuntimeDetectionMode.Onset,
                    RuntimeDifficulty.Normal,
                    RuntimeCombatStyle.Legacy);
            }
        }

        public RuntimeDetectionMode DetectionMode { get; }

        public RuntimeDifficulty Difficulty { get; }

        public RuntimeCombatStyle CombatStyle { get; }
    }

    public enum RuntimeDetectionMode
    {
        Amplitude,
        Onset
    }

    public enum RuntimeDifficulty
    {
        Easy,
        Normal,
        Hard
    }

    public enum RuntimeCombatStyle
    {
        Legacy,
        Balanced,
        Defensive,
        Aggressive,
        Bursty
    }
}
