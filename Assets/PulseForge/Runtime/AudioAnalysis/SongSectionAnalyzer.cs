using System;
using System.Collections.Generic;

namespace PulseForge.AudioAnalysis
{
    internal static class SongSectionAnalyzer
    {
        private const double DefaultBlockSeconds = 2d;
        private const double MinimumSectionSeconds = 3d;
        private const double BoundarySnapSeconds = 0.35d;

        public static List<SongSectionData> Build(
            IReadOnlyList<AudioFeatureFrame> frames,
            IReadOnlyList<OnsetCandidateData> candidates,
            BeatGridData grid,
            double durationSeconds,
            double hopSeconds)
        {
            List<SongSectionData> sections = new List<SongSectionData>();
            if (durationSeconds <= 0d)
            {
                return sections;
            }

            double blockSeconds = grid != null && grid.beatIntervalSeconds > 0d
                ? Math.Max(1.5d, Math.Min(4d, grid.beatIntervalSeconds * 4d))
                : DefaultBlockSeconds;
            List<BlockSummary> blocks = BuildBlocks(
                frames,
                candidates,
                durationSeconds,
                blockSeconds);
            NormalizeBlockActivity(blocks);
            List<double> boundaries = FindBoundaries(blocks, grid, durationSeconds);

            for (int i = 0; i < boundaries.Count - 1; i++)
            {
                sections.Add(BuildSection(
                    frames,
                    candidates,
                    grid,
                    boundaries[i],
                    boundaries[i + 1],
                    hopSeconds));
            }

            return sections;
        }

        private static List<BlockSummary> BuildBlocks(
            IReadOnlyList<AudioFeatureFrame> frames,
            IReadOnlyList<OnsetCandidateData> candidates,
            double durationSeconds,
            double blockSeconds)
        {
            int blockCount = Math.Max(1, (int)Math.Ceiling(durationSeconds / blockSeconds));
            List<BlockSummary> blocks = new List<BlockSummary>(blockCount);
            int frameIndex = 0;
            int candidateIndex = 0;

            for (int blockIndex = 0; blockIndex < blockCount; blockIndex++)
            {
                double start = blockIndex * blockSeconds;
                double end = Math.Min(durationSeconds, start + blockSeconds);
                BlockSummary block = new BlockSummary(start, end);

                while (frameIndex < frames.Count && frames[frameIndex].timeSeconds < start)
                {
                    frameIndex++;
                }

                int localFrameIndex = frameIndex;
                while (localFrameIndex < frames.Count && frames[localFrameIndex].timeSeconds < end)
                {
                    block.AddFrame(frames[localFrameIndex]);
                    localFrameIndex++;
                }

                frameIndex = localFrameIndex;
                while (candidateIndex < candidates.Count && candidates[candidateIndex].timeSeconds < start)
                {
                    candidateIndex++;
                }

                int localCandidateIndex = candidateIndex;
                while (localCandidateIndex < candidates.Count
                    && candidates[localCandidateIndex].timeSeconds < end)
                {
                    block.CandidateCount++;
                    localCandidateIndex++;
                }

                candidateIndex = localCandidateIndex;
                block.FinalizeValues();
                blocks.Add(block);
            }

            return blocks;
        }

        private static void NormalizeBlockActivity(List<BlockSummary> blocks)
        {
            double[] rmsValues = new double[blocks.Count];
            double maximumOnsetRate = 0d;
            for (int i = 0; i < blocks.Count; i++)
            {
                rmsValues[i] = blocks[i].AverageRms;
                maximumOnsetRate = Math.Max(maximumOnsetRate, blocks[i].OnsetRate);
            }

            double lowRms = AnalyzerV2Math.Percentile(rmsValues, rmsValues.Length, 0.20d);
            for (int i = 0; i < blocks.Count; i++)
            {
                rmsValues[i] = blocks[i].AverageRms;
            }

            double highRms = AnalyzerV2Math.Percentile(rmsValues, rmsValues.Length, 0.90d);
            double rmsRange = Math.Max(0.000001d, highRms - lowRms);
            for (int i = 0; i < blocks.Count; i++)
            {
                BlockSummary block = blocks[i];
                double energyScore = AnalyzerV2Math.Clamp01((block.AverageRms - lowRms) / rmsRange);
                double onsetScore = maximumOnsetRate <= 0d
                    ? 0d
                    : AnalyzerV2Math.Clamp01(block.OnsetRate / maximumOnsetRate);
                double activity = AnalyzerV2Math.Clamp01(
                    (energyScore * 0.55d)
                    + (onsetScore * 0.30d)
                    + (block.AverageContrast * 0.15d));
                if (block.SilentFraction >= 0.75d)
                {
                    activity = 0d;
                }

                block.Activity = activity;
                block.Level = Classify(activity, block.SilentFraction);
            }
        }

        private static List<double> FindBoundaries(
            IReadOnlyList<BlockSummary> blocks,
            BeatGridData grid,
            double durationSeconds)
        {
            List<double> rawBoundaries = new List<double> { 0d };
            for (int i = 1; i < blocks.Count; i++)
            {
                BlockSummary previous = blocks[i - 1];
                BlockSummary current = blocks[i];
                double descriptorDistance = Math.Abs(current.Activity - previous.Activity)
                    + (Math.Abs(current.LowRatio - previous.LowRatio) * 0.20d)
                    + (Math.Abs(current.MidRatio - previous.MidRatio) * 0.20d)
                    + (Math.Abs(current.HighRatio - previous.HighRatio) * 0.20d)
                    + (Math.Abs(current.OnsetRate - previous.OnsetRate) * 0.10d);
                bool meaningfulLevelChange = current.Level != previous.Level
                    && (current.Level == SongSectionActivityLevel.Silent
                        || previous.Level == SongSectionActivityLevel.Silent
                        || Math.Abs(current.Activity - previous.Activity) >= 0.18d);
                if (meaningfulLevelChange || descriptorDistance >= 0.45d)
                {
                    rawBoundaries.Add(SnapBoundary(current.StartSeconds, grid));
                }
            }

            rawBoundaries.Add(durationSeconds);
            List<double> boundaries = new List<double> { 0d };
            for (int i = 1; i < rawBoundaries.Count - 1; i++)
            {
                double boundary = Math.Max(0d, Math.Min(durationSeconds, rawBoundaries[i]));
                if (boundary - boundaries[boundaries.Count - 1] >= MinimumSectionSeconds)
                {
                    boundaries.Add(boundary);
                }
            }

            if (durationSeconds - boundaries[boundaries.Count - 1] < MinimumSectionSeconds
                && boundaries.Count > 1)
            {
                boundaries.RemoveAt(boundaries.Count - 1);
            }

            boundaries.Add(durationSeconds);
            return boundaries;
        }

        private static double SnapBoundary(double timeSeconds, BeatGridData grid)
        {
            if (grid?.beatTimesSeconds == null || grid.beatTimesSeconds.Count == 0)
            {
                return timeSeconds;
            }

            double best = timeSeconds;
            double bestDistance = BoundarySnapSeconds;
            for (int i = 0; i < grid.beatTimesSeconds.Count; i += 4)
            {
                double distance = Math.Abs(grid.beatTimesSeconds[i] - timeSeconds);
                if (distance < bestDistance)
                {
                    best = grid.beatTimesSeconds[i];
                    bestDistance = distance;
                }
            }

            if (best != timeSeconds)
            {
                return best;
            }

            for (int i = 0; i < grid.beatTimesSeconds.Count; i++)
            {
                double distance = Math.Abs(grid.beatTimesSeconds[i] - timeSeconds);
                if (distance < bestDistance)
                {
                    best = grid.beatTimesSeconds[i];
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static SongSectionData BuildSection(
            IReadOnlyList<AudioFeatureFrame> frames,
            IReadOnlyList<OnsetCandidateData> candidates,
            BeatGridData grid,
            double start,
            double end,
            double hopSeconds)
        {
            SongSectionData section = new SongSectionData
            {
                startTimeSeconds = start,
                endTimeSeconds = end,
                tempoConfidence = grid?.tempoConfidence ?? 0d
            };

            int frameCount = 0;
            int silentCount = 0;
            double contrastTotal = 0d;
            for (int i = 0; i < frames.Count; i++)
            {
                AudioFeatureFrame frame = frames[i];
                if (frame.timeSeconds < start || frame.timeSeconds >= end)
                {
                    continue;
                }

                frameCount++;
                section.averageRms += frame.rms;
                section.averageLowLogEnergy += frame.lowLogEnergy;
                section.averageMidLogEnergy += frame.midLogEnergy;
                section.averageHighLogEnergy += frame.highLogEnergy;
                contrastTotal += frame.localContrast;
                if (frame.isSilent)
                {
                    silentCount++;
                }
            }

            if (frameCount > 0)
            {
                section.averageRms /= frameCount;
                section.averageLowLogEnergy /= frameCount;
                section.averageMidLogEnergy /= frameCount;
                section.averageHighLogEnergy /= frameCount;
            }

            double alignmentTotal = 0d;
            for (int i = 0; i < candidates.Count; i++)
            {
                OnsetCandidateData candidate = candidates[i];
                if (candidate.timeSeconds < start || candidate.timeSeconds >= end)
                {
                    continue;
                }

                section.candidateCount++;
                alignmentTotal += candidate.beatAlignment;
            }

            double duration = Math.Max(hopSeconds, end - start);
            double silentFraction = frameCount == 0 ? 1d : silentCount / (double)frameCount;
            double onsetScore = AnalyzerV2Math.Clamp01(section.candidateCount / duration / 2d);
            double energyScore = AnalyzerV2Math.Clamp01(section.averageRms / 0.15d);
            double contrastScore = frameCount == 0 ? 0d : contrastTotal / frameCount;
            section.activity = silentFraction >= 0.75d
                ? 0d
                : AnalyzerV2Math.Clamp01(
                    (energyScore * 0.55d) + (onsetScore * 0.30d) + (contrastScore * 0.15d));
            section.activityLevel = Classify(section.activity, silentFraction);
            section.gridConfidence = section.candidateCount > 0
                ? AnalyzerV2Math.Clamp01(alignmentTotal / section.candidateCount)
                : (grid?.gridConfidence ?? 0d) * 0.5d;
            return section;
        }

        private static SongSectionActivityLevel Classify(double activity, double silentFraction)
        {
            if (silentFraction >= 0.75d)
            {
                return SongSectionActivityLevel.Silent;
            }

            if (activity >= 0.78d)
            {
                return SongSectionActivityLevel.Peak;
            }

            return activity >= 0.45d
                ? SongSectionActivityLevel.Active
                : SongSectionActivityLevel.Low;
        }

        private sealed class BlockSummary
        {
            private int frameCount;
            private int silentFrameCount;
            private double rmsTotal;
            private double lowTotal;
            private double midTotal;
            private double highTotal;
            private double contrastTotal;

            public BlockSummary(double startSeconds, double endSeconds)
            {
                StartSeconds = startSeconds;
                EndSeconds = endSeconds;
            }

            public double StartSeconds { get; }

            public double EndSeconds { get; }

            public int CandidateCount { get; set; }

            public double AverageRms { get; private set; }

            public double AverageContrast { get; private set; }

            public double SilentFraction { get; private set; }

            public double LowRatio { get; private set; }

            public double MidRatio { get; private set; }

            public double HighRatio { get; private set; }

            public double OnsetRate { get; private set; }

            public double Activity { get; set; }

            public SongSectionActivityLevel Level { get; set; }

            public void AddFrame(AudioFeatureFrame frame)
            {
                frameCount++;
                rmsTotal += frame.rms;
                lowTotal += frame.lowLogEnergy;
                midTotal += frame.midLogEnergy;
                highTotal += frame.highLogEnergy;
                contrastTotal += frame.localContrast;
                if (frame.isSilent)
                {
                    silentFrameCount++;
                }
            }

            public void FinalizeValues()
            {
                if (frameCount > 0)
                {
                    AverageRms = rmsTotal / frameCount;
                    AverageContrast = contrastTotal / frameCount;
                    SilentFraction = silentFrameCount / (double)frameCount;
                }

                double bandTotal = lowTotal + midTotal + highTotal;
                if (bandTotal > 0d)
                {
                    LowRatio = lowTotal / bandTotal;
                    MidRatio = midTotal / bandTotal;
                    HighRatio = highTotal / bandTotal;
                }

                OnsetRate = CandidateCount / Math.Max(0.001d, EndSeconds - StartSeconds);
            }
        }
    }
}
