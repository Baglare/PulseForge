using System;
using System.Collections.Generic;

namespace PulseForge.AudioAnalysis
{
    internal static class AdaptiveOnsetDetector
    {
        private const double ThresholdMadMultiplier = 3d;
        private const double MinimumThreshold = 0.00000001d;
        private const double MinimumNoiseFloor = 0.0005d;
        private const int StatisticsStrideFrames = 5;

        public static List<OnsetCandidateData> Detect(
            List<AudioFeatureFrame> frames,
            double hopSeconds)
        {
            return Detect(frames, hopSeconds, AudioCandidateDetectionMode.Onset);
        }

        public static List<OnsetCandidateData> Detect(
            List<AudioFeatureFrame> frames,
            double hopSeconds,
            AudioCandidateDetectionMode detectionMode)
        {
            if (frames == null)
            {
                throw new ArgumentNullException(nameof(frames));
            }

            if (frames.Count == 0)
            {
                return new List<OnsetCandidateData>();
            }

            ApplyLocalStatistics(frames, hopSeconds);
            MarkSilentRegions(frames, hopSeconds);
            List<BandPeak> bandPeaks = detectionMode == AudioCandidateDetectionMode.Amplitude
                ? FindAmplitudePeaks(frames)
                : FindBandPeaks(frames);
            return MergeBandPeaks(bandPeaks);
        }

        private static List<BandPeak> FindAmplitudePeaks(List<AudioFeatureFrame> frames)
        {
            List<BandPeak> peaks = new List<BandPeak>();
            for (int i = 1; i < frames.Count - 1; i++)
            {
                AudioFeatureFrame frame = frames[i];
                double threshold = Math.Max(
                    MinimumNoiseFloor,
                    frame.localNoiseFloor * 1.35d);
                if (frame.isSilent
                    || frame.rms <= threshold
                    || frame.rms < frames[i - 1].rms
                    || frame.rms <= frames[i + 1].rms)
                {
                    continue;
                }

                double strength = AnalyzerV2Math.Clamp01(
                    (frame.rms - threshold) / (frame.rms + threshold + MinimumThreshold));
                double maximumEnergy = Math.Max(
                    frame.lowLogEnergy,
                    Math.Max(frame.midLogEnergy, frame.highLogEnergy));
                if (maximumEnergy <= 0d)
                {
                    peaks.Add(new BandPeak(
                        frame.timeSeconds,
                        AudioBandMask.Mid,
                        strength,
                        frame.localContrast));
                    continue;
                }

                double supportFloor = maximumEnergy * 0.72d;
                TryAddAmplitudeBand(peaks, frame, AudioBandMask.Low, frame.lowLogEnergy, supportFloor, strength);
                TryAddAmplitudeBand(peaks, frame, AudioBandMask.Mid, frame.midLogEnergy, supportFloor, strength);
                TryAddAmplitudeBand(peaks, frame, AudioBandMask.High, frame.highLogEnergy, supportFloor, strength);
            }

            return peaks;
        }

        private static void TryAddAmplitudeBand(
            ICollection<BandPeak> peaks,
            AudioFeatureFrame frame,
            AudioBandMask band,
            double energy,
            double supportFloor,
            double strength)
        {
            if (energy + MinimumThreshold < supportFloor)
            {
                return;
            }

            peaks.Add(new BandPeak(
                frame.timeSeconds,
                band,
                strength,
                Math.Max(frame.localContrast, strength)));
        }

        private static void ApplyLocalStatistics(List<AudioFeatureFrame> frames, double hopSeconds)
        {
            int localFrameCount = Math.Max(
                3,
                (int)Math.Round(AnalyzerV2Defaults.LocalWindowSeconds / hopSeconds));
            if ((localFrameCount & 1) == 0)
            {
                localFrameCount++;
            }

            int radius = localFrameCount / 2;
            double[] values = new double[localFrameCount];
            double[] deviations = new double[localFrameCount];

            for (int blockStart = 0; blockStart < frames.Count; blockStart += StatisticsStrideFrames)
            {
                int blockEnd = Math.Min(frames.Count, blockStart + StatisticsStrideFrames);
                int centerIndex = blockStart + ((blockEnd - blockStart) / 2);
                int start = Math.Max(0, centerIndex - radius);
                int end = Math.Min(frames.Count - 1, centerIndex + radius);
                int count = end - start + 1;

                ComputeMetricStatistics(
                    frames,
                    start,
                    count,
                    FeatureMetric.LowFlux,
                    values,
                    deviations,
                    out double lowMedian,
                    out double lowMad);
                ComputeMetricStatistics(
                    frames,
                    start,
                    count,
                    FeatureMetric.MidFlux,
                    values,
                    deviations,
                    out double midMedian,
                    out double midMad);
                ComputeMetricStatistics(
                    frames,
                    start,
                    count,
                    FeatureMetric.HighFlux,
                    values,
                    deviations,
                    out double highMedian,
                    out double highMad);
                ComputeMetricStatistics(
                    frames,
                    start,
                    count,
                    FeatureMetric.Rms,
                    values,
                    deviations,
                    out double rmsMedian,
                    out double rmsMad);

                double lowThreshold = BuildThreshold(lowMedian, lowMad);
                double midThreshold = BuildThreshold(midMedian, midMad);
                double highThreshold = BuildThreshold(highMedian, highMad);
                double noiseFloor = Math.Max(
                    MinimumNoiseFloor,
                    (rmsMedian * 0.35d) + (rmsMad * 1.5d));
                for (int frameIndex = blockStart; frameIndex < blockEnd; frameIndex++)
                {
                    AudioFeatureFrame frame = frames[frameIndex];
                    frame.lowOnsetThreshold = lowThreshold;
                    frame.midOnsetThreshold = midThreshold;
                    frame.highOnsetThreshold = highThreshold;
                    frame.localNoiseFloor = noiseFloor;
                    frame.localContrast = Math.Max(
                        ComputeContrast(frame.lowFlux, lowThreshold),
                        Math.Max(
                            ComputeContrast(frame.midFlux, midThreshold),
                            ComputeContrast(frame.highFlux, highThreshold)));
                }
            }
        }

        private static void MarkSilentRegions(List<AudioFeatureFrame> frames, double hopSeconds)
        {
            int minimumSilentFrames = Math.Max(
                1,
                (int)Math.Ceiling(AnalyzerV2Defaults.MinimumSilentRegionSeconds / hopSeconds));
            int quietRunStart = -1;

            for (int i = 0; i <= frames.Count; i++)
            {
                bool quiet = i < frames.Count && IsQuiet(frames[i]);
                if (quiet)
                {
                    if (quietRunStart < 0)
                    {
                        quietRunStart = i;
                    }

                    continue;
                }

                if (quietRunStart < 0)
                {
                    continue;
                }

                int runLength = i - quietRunStart;
                if (runLength >= minimumSilentFrames)
                {
                    for (int frameIndex = quietRunStart; frameIndex < i; frameIndex++)
                    {
                        frames[frameIndex].isSilent = true;
                    }
                }

                quietRunStart = -1;
            }
        }

        private static List<BandPeak> FindBandPeaks(List<AudioFeatureFrame> frames)
        {
            List<BandPeak> peaks = new List<BandPeak>();
            for (int i = 1; i < frames.Count - 1; i++)
            {
                AudioFeatureFrame frame = frames[i];
                if (frame.isSilent)
                {
                    continue;
                }

                TryAddPeak(
                    peaks,
                    frames[i - 1].lowFlux,
                    frame.lowFlux,
                    frames[i + 1].lowFlux,
                    frame.lowOnsetThreshold,
                    frame.timeSeconds,
                    AudioBandMask.Low,
                    frame.localContrast);
                TryAddPeak(
                    peaks,
                    frames[i - 1].midFlux,
                    frame.midFlux,
                    frames[i + 1].midFlux,
                    frame.midOnsetThreshold,
                    frame.timeSeconds,
                    AudioBandMask.Mid,
                    frame.localContrast);
                TryAddPeak(
                    peaks,
                    frames[i - 1].highFlux,
                    frame.highFlux,
                    frames[i + 1].highFlux,
                    frame.highOnsetThreshold,
                    frame.timeSeconds,
                    AudioBandMask.High,
                    frame.localContrast);
            }

            return peaks;
        }

        private static List<OnsetCandidateData> MergeBandPeaks(List<BandPeak> peaks)
        {
            List<OnsetCandidateData> candidates = new List<OnsetCandidateData>();
            if (peaks.Count == 0)
            {
                return candidates;
            }

            CandidateAccumulator accumulator = new CandidateAccumulator(peaks[0]);
            for (int i = 1; i < peaks.Count; i++)
            {
                BandPeak peak = peaks[i];
                if (peak.TimeSeconds - accumulator.LastPeakTimeSeconds
                    <= AnalyzerV2Defaults.CandidateMergeSeconds)
                {
                    accumulator.Add(peak);
                    continue;
                }

                candidates.Add(accumulator.ToCandidate());
                accumulator = new CandidateAccumulator(peak);
            }

            candidates.Add(accumulator.ToCandidate());
            return candidates;
        }

        private static void TryAddPeak(
            ICollection<BandPeak> peaks,
            double previous,
            double current,
            double next,
            double threshold,
            double timeSeconds,
            AudioBandMask band,
            double contrast)
        {
            if (current <= threshold || current < previous || current <= next)
            {
                return;
            }

            double strength = AnalyzerV2Math.Clamp01(
                (current - threshold) / (current + threshold + MinimumThreshold));
            peaks.Add(new BandPeak(timeSeconds, band, strength, contrast));
        }

        private static bool IsQuiet(AudioFeatureFrame frame)
        {
            return frame.rms <= frame.localNoiseFloor
                && frame.lowFlux <= frame.lowOnsetThreshold
                && frame.midFlux <= frame.midOnsetThreshold
                && frame.highFlux <= frame.highOnsetThreshold;
        }

        private static double BuildThreshold(double median, double mad)
        {
            double robustSpread = Math.Max(mad, median * 0.10d);
            return median + (ThresholdMadMultiplier * robustSpread) + MinimumThreshold;
        }

        private static double ComputeContrast(double value, double threshold)
        {
            if (value <= threshold)
            {
                return 0d;
            }

            return AnalyzerV2Math.Clamp01(
                (value - threshold) / (value + threshold + MinimumThreshold));
        }

        private static void ComputeMetricStatistics(
            IReadOnlyList<AudioFeatureFrame> frames,
            int start,
            int count,
            FeatureMetric metric,
            double[] values,
            double[] deviations,
            out double median,
            out double mad)
        {
            for (int i = 0; i < count; i++)
            {
                AudioFeatureFrame frame = frames[start + i];
                switch (metric)
                {
                    case FeatureMetric.LowFlux:
                        values[i] = frame.lowFlux;
                        break;
                    case FeatureMetric.MidFlux:
                        values[i] = frame.midFlux;
                        break;
                    case FeatureMetric.HighFlux:
                        values[i] = frame.highFlux;
                        break;
                    default:
                        values[i] = frame.rms;
                        break;
                }
            }

            AnalyzerV2Math.MedianAndMad(values, deviations, count, out median, out mad);
        }

        private enum FeatureMetric
        {
            LowFlux,
            MidFlux,
            HighFlux,
            Rms
        }

        private readonly struct BandPeak
        {
            public BandPeak(
                double timeSeconds,
                AudioBandMask band,
                double strength,
                double contrast)
            {
                TimeSeconds = timeSeconds;
                Band = band;
                Strength = strength;
                Contrast = contrast;
            }

            public double TimeSeconds { get; }

            public AudioBandMask Band { get; }

            public double Strength { get; }

            public double Contrast { get; }
        }

        private sealed class CandidateAccumulator
        {
            private double weightedTimeTotal;
            private double weightTotal;
            private double maximumContrast;
            private AudioBandMask bands;
            private double lowStrength;
            private double midStrength;
            private double highStrength;

            public CandidateAccumulator(BandPeak peak)
            {
                Add(peak);
            }

            public double LastPeakTimeSeconds { get; private set; }

            public void Add(BandPeak peak)
            {
                double weight = Math.Max(0.000001d, peak.Strength);
                weightedTimeTotal += peak.TimeSeconds * weight;
                weightTotal += weight;
                maximumContrast = Math.Max(maximumContrast, peak.Contrast);
                bands |= peak.Band;
                if (peak.Band == AudioBandMask.Low)
                {
                    lowStrength = Math.Max(lowStrength, peak.Strength);
                }
                else if (peak.Band == AudioBandMask.Mid)
                {
                    midStrength = Math.Max(midStrength, peak.Strength);
                }
                else if (peak.Band == AudioBandMask.High)
                {
                    highStrength = Math.Max(highStrength, peak.Strength);
                }

                LastPeakTimeSeconds = peak.TimeSeconds;
            }

            public OnsetCandidateData ToCandidate()
            {
                int bandCount = CountBands(bands);
                double combinedStrength = 1d
                    - ((1d - lowStrength) * (1d - midStrength) * (1d - highStrength));
                double supportScore = bandCount / 3d;
                double baseConfidence = AnalyzerV2Math.Clamp01(
                    (combinedStrength * 0.55d)
                    + (supportScore * 0.20d)
                    + (maximumContrast * 0.25d));
                return new OnsetCandidateData
                {
                    timeSeconds = weightedTimeTotal / weightTotal,
                    confidence = baseConfidence,
                    strength = combinedStrength,
                    localContrast = maximumContrast,
                    supportingBands = bands,
                    lowStrength = lowStrength,
                    midStrength = midStrength,
                    highStrength = highStrength,
                    selectedByAdaptiveThreshold = true
                };
            }

            private static int CountBands(AudioBandMask mask)
            {
                int count = 0;
                if ((mask & AudioBandMask.Low) != 0)
                {
                    count++;
                }

                if ((mask & AudioBandMask.Mid) != 0)
                {
                    count++;
                }

                if ((mask & AudioBandMask.High) != 0)
                {
                    count++;
                }

                return count;
            }
        }
    }
}
