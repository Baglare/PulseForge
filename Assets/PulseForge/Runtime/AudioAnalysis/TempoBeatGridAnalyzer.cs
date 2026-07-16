using System;
using System.Collections.Generic;

namespace PulseForge.AudioAnalysis
{
    internal static class TempoBeatGridAnalyzer
    {
        private const int PhaseBinCount = 64;
        private const double BoundaryTolerance = 0.000000001d;

        public static BeatGridData Analyze(
            IReadOnlyList<AudioFeatureFrame> frames,
            IList<OnsetCandidateData> candidates,
            double durationSeconds,
            double hopSeconds)
        {
            BeatGridData grid = new BeatGridData();
            if (frames == null || frames.Count < 2 || hopSeconds <= 0d)
            {
                ApplyCandidateConfidence(candidates, grid);
                return grid;
            }

            double[] envelope = BuildCombinedEnvelope(frames);
            int minimumLag = Math.Max(
                1,
                (int)Math.Floor(60d / (AnalyzerV2Defaults.MaximumTempoBpm * hopSeconds)));
            int maximumLag = Math.Min(
                frames.Count - 1,
                (int)Math.Ceiling(60d / (AnalyzerV2Defaults.MinimumTempoBpm * hopSeconds)));
            if (maximumLag < minimumLag)
            {
                ApplyCandidateConfidence(candidates, grid);
                return grid;
            }

            double[] phaseHistogram = new double[PhaseBinCount];
            TempoCandidate best = default(TempoCandidate);
            TempoCandidate second = default(TempoCandidate);
            bool hasBest = false;
            bool hasSecond = false;

            for (int lag = minimumLag; lag <= maximumLag; lag++)
            {
                double bpm = 60d / (lag * hopSeconds);
                if (bpm < AnalyzerV2Defaults.MinimumTempoBpm - BoundaryTolerance
                    || bpm > AnalyzerV2Defaults.MaximumTempoBpm + BoundaryTolerance)
                {
                    continue;
                }

                double interval = lag * hopSeconds;
                double autocorrelation = ComputeNormalizedAutocorrelation(envelope, lag);
                double phase = FindBestPhase(
                    candidates,
                    interval,
                    phaseHistogram,
                    out double alignment);
                TempoCandidate tempoCandidate = new TempoCandidate(
                    bpm,
                    interval,
                    phase,
                    autocorrelation,
                    alignment,
                    (autocorrelation * 0.70d) + (alignment * 0.30d));

                if (!hasBest || IsBetter(tempoCandidate, best))
                {
                    if (hasBest)
                    {
                        second = best;
                        hasSecond = true;
                    }

                    best = tempoCandidate;
                    hasBest = true;
                }
                else if ((!hasSecond || IsBetter(tempoCandidate, second))
                    && Math.Abs(tempoCandidate.Bpm - best.Bpm) > 2d)
                {
                    second = tempoCandidate;
                    hasSecond = true;
                }
            }

            if (!hasBest)
            {
                ApplyCandidateConfidence(candidates, grid);
                return grid;
            }

            double margin = hasSecond
                ? Math.Max(0d, best.Score - second.Score)
                : best.Score;
            double candidateCoverage = AnalyzerV2Math.Clamp01((candidates?.Count ?? 0) / 8d);
            double confidence = AnalyzerV2Math.Clamp01(
                (best.Autocorrelation * 0.50d)
                + (best.Alignment * 0.25d)
                + (margin * 0.15d)
                + (candidateCoverage * 0.10d));
            if ((candidates?.Count ?? 0) < 3)
            {
                confidence *= 0.5d;
            }

            if (best.Autocorrelation < 0.05d)
            {
                confidence *= 0.5d;
            }

            grid.bpm = best.Bpm;
            grid.tempoConfidence = AnalyzerV2Math.Clamp01(confidence);
            grid.beatIntervalSeconds = best.IntervalSeconds;
            grid.phaseSeconds = best.PhaseSeconds;
            grid.gridStrength = best.Alignment;
            grid.gridConfidence = AnalyzerV2Math.Clamp01(
                (grid.tempoConfidence * 0.65d) + (best.Alignment * 0.35d));
            BuildGridTimes(grid, durationSeconds);
            ApplyCandidateConfidence(candidates, grid);
            return grid;
        }

        private static double[] BuildCombinedEnvelope(IReadOnlyList<AudioFeatureFrame> frames)
        {
            double[] envelope = new double[frames.Count];
            for (int i = 0; i < frames.Count; i++)
            {
                AudioFeatureFrame frame = frames[i];
                if (frame.isSilent)
                {
                    continue;
                }

                double low = ComputeBandNovelty(frame.lowFlux, frame.lowOnsetThreshold);
                double mid = ComputeBandNovelty(frame.midFlux, frame.midOnsetThreshold);
                double high = ComputeBandNovelty(frame.highFlux, frame.highOnsetThreshold);
                envelope[i] = AnalyzerV2Math.Clamp01(
                    Math.Max(low, Math.Max(mid, high))
                    + (Math.Min(1d, low + mid + high) * 0.25d));
            }

            return envelope;
        }

        private static double ComputeBandNovelty(double value, double threshold)
        {
            if (value <= threshold)
            {
                return 0d;
            }

            return AnalyzerV2Math.Clamp01(
                (value - threshold) / (value + threshold + 0.00000001d));
        }

        private static double ComputeNormalizedAutocorrelation(double[] envelope, int lag)
        {
            double numerator = 0d;
            double leftEnergy = 0d;
            double rightEnergy = 0d;
            for (int i = lag; i < envelope.Length; i++)
            {
                double left = envelope[i];
                double right = envelope[i - lag];
                numerator += left * right;
                leftEnergy += left * left;
                rightEnergy += right * right;
            }

            double denominator = Math.Sqrt(leftEnergy * rightEnergy);
            return denominator <= 0.000000000001d
                ? 0d
                : AnalyzerV2Math.Clamp01(numerator / denominator);
        }

        private static double FindBestPhase(
            IList<OnsetCandidateData> candidates,
            double intervalSeconds,
            double[] histogram,
            out double alignment)
        {
            Array.Clear(histogram, 0, histogram.Length);
            if (candidates == null || candidates.Count == 0 || intervalSeconds <= 0d)
            {
                alignment = 0d;
                return 0d;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                OnsetCandidateData candidate = candidates[i];
                double phase = PositiveModulo(candidate.timeSeconds, intervalSeconds);
                int bin = Math.Min(
                    histogram.Length - 1,
                    (int)Math.Floor(phase / intervalSeconds * histogram.Length));
                histogram[bin] += Math.Max(0.05d, candidate.confidence);
            }

            int bestBin = 0;
            for (int i = 1; i < histogram.Length; i++)
            {
                if (histogram[i] > histogram[bestBin])
                {
                    bestBin = i;
                }
            }

            double phaseSeconds = (bestBin + 0.5d) * intervalSeconds / histogram.Length;
            double offsetTotal = 0d;
            double weightTotal = 0d;
            for (int i = 0; i < candidates.Count; i++)
            {
                OnsetCandidateData candidate = candidates[i];
                double offset = SignedGridOffset(candidate.timeSeconds, phaseSeconds, intervalSeconds);
                if (Math.Abs(offset) > intervalSeconds * 0.25d)
                {
                    continue;
                }

                double weight = Math.Max(0.05d, candidate.confidence);
                offsetTotal += offset * weight;
                weightTotal += weight;
            }

            if (weightTotal > 0d)
            {
                phaseSeconds = PositiveModulo(phaseSeconds + (offsetTotal / weightTotal), intervalSeconds);
            }

            double alignmentTotal = 0d;
            double alignmentWeight = 0d;
            for (int i = 0; i < candidates.Count; i++)
            {
                OnsetCandidateData candidate = candidates[i];
                double weight = Math.Max(0.05d, candidate.confidence);
                double offset = Math.Abs(SignedGridOffset(
                    candidate.timeSeconds,
                    phaseSeconds,
                    intervalSeconds));
                double score = AnalyzerV2Math.Clamp01(1d - (offset / (intervalSeconds * 0.5d)));
                alignmentTotal += score * weight;
                alignmentWeight += weight;
            }

            alignment = alignmentWeight <= 0d
                ? 0d
                : AnalyzerV2Math.Clamp01(alignmentTotal / alignmentWeight);
            return phaseSeconds;
        }

        private static void BuildGridTimes(BeatGridData grid, double durationSeconds)
        {
            if (grid.beatIntervalSeconds <= 0d || durationSeconds <= 0d)
            {
                return;
            }

            double beatTime = grid.phaseSeconds;
            while (beatTime - grid.beatIntervalSeconds >= 0d)
            {
                beatTime -= grid.beatIntervalSeconds;
            }

            for (; beatTime <= durationSeconds + BoundaryTolerance; beatTime += grid.beatIntervalSeconds)
            {
                if (beatTime >= -BoundaryTolerance)
                {
                    grid.beatTimesSeconds.Add(Math.Max(0d, beatTime));
                }

                double subdivision = beatTime + (grid.beatIntervalSeconds * 0.5d);
                if (subdivision >= 0d && subdivision <= durationSeconds + BoundaryTolerance)
                {
                    grid.subdivisionTimesSeconds.Add(subdivision);
                }
            }
        }

        private static void ApplyCandidateConfidence(
            IList<OnsetCandidateData> candidates,
            BeatGridData grid)
        {
            if (candidates == null)
            {
                return;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                OnsetCandidateData candidate = candidates[i];
                int supportCount = CountBands(candidate.supportingBands);
                candidate.beatAlignment = grid.beatIntervalSeconds <= 0d
                    ? 0d
                    : AnalyzerV2Math.Clamp01(
                        1d - (Math.Abs(SignedGridOffset(
                            candidate.timeSeconds,
                            grid.phaseSeconds,
                            grid.beatIntervalSeconds))
                            / (grid.beatIntervalSeconds * 0.5d)));
                candidate.confidence = AnalyzerV2Math.Clamp01(
                    (candidate.strength * 0.40d)
                    + ((supportCount / 3d) * 0.20d)
                    + (candidate.localContrast * 0.20d)
                    + (candidate.beatAlignment * 0.20d));
            }
        }

        private static bool IsBetter(TempoCandidate candidate, TempoCandidate current)
        {
            if (candidate.Score > current.Score + BoundaryTolerance)
            {
                return true;
            }

            if (Math.Abs(candidate.Score - current.Score) <= BoundaryTolerance)
            {
                if (candidate.Alignment > current.Alignment + BoundaryTolerance)
                {
                    return true;
                }

                if (Math.Abs(candidate.Alignment - current.Alignment) <= BoundaryTolerance)
                {
                    return candidate.Autocorrelation > current.Autocorrelation;
                }
            }

            return false;
        }

        private static double SignedGridOffset(double time, double phase, double interval)
        {
            double offset = PositiveModulo(time - phase, interval);
            return offset > interval * 0.5d ? offset - interval : offset;
        }

        private static double PositiveModulo(double value, double modulus)
        {
            if (modulus <= 0d)
            {
                return 0d;
            }

            double result = value % modulus;
            return result < 0d ? result + modulus : result;
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

        private readonly struct TempoCandidate
        {
            public TempoCandidate(
                double bpm,
                double intervalSeconds,
                double phaseSeconds,
                double autocorrelation,
                double alignment,
                double score)
            {
                Bpm = bpm;
                IntervalSeconds = intervalSeconds;
                PhaseSeconds = phaseSeconds;
                Autocorrelation = autocorrelation;
                Alignment = alignment;
                Score = score;
            }

            public double Bpm { get; }

            public double IntervalSeconds { get; }

            public double PhaseSeconds { get; }

            public double Autocorrelation { get; }

            public double Alignment { get; }

            public double Score { get; }
        }
    }
}
