using System;
using System.Collections.Generic;

namespace PulseForge.AudioAnalysis
{
    public static class RadialAudioAnalyzerV2
    {
        private const double LowMinimumHz = 20d;
        private const double LowMaximumHz = 180d;
        private const double MidMaximumHz = 2000d;
        private const double HighMaximumHz = 10000d;
        private const double HannScale = 2d * Math.PI / (AnalyzerV2Defaults.FftSize - 1d);

        public static RadialAudioAnalysisResult Analyze(IAudioSampleSource source)
        {
            return Analyze(source, AudioCandidateDetectionMode.Onset);
        }

        public static RadialAudioAnalysisResult Analyze(
            IAudioSampleSource source,
            AudioCandidateDetectionMode detectionMode)
        {
            AnalysisJob job = BeginAnalyze(source, detectionMode);
            while (!job.FeatureExtractionComplete)
            {
                job.StepFeatureExtraction(1);
            }

            return job.CompleteAnalysis();
        }

        public static AnalysisJob BeginAnalyze(
            IAudioSampleSource source,
            AudioCandidateDetectionMode detectionMode = AudioCandidateDetectionMode.Onset)
        {
            ValidateSource(source);
            return new AnalysisJob(source, detectionMode);
        }

        private static RadialAudioAnalysisResult BuildResult(
            List<AudioFeatureFrame> frames,
            int hopSizeSamples,
            int sampleRate,
            double durationSeconds,
            AudioCandidateDetectionMode detectionMode)
        {
            double hopSeconds = hopSizeSamples / (double)sampleRate;
            List<OnsetCandidateData> candidates = AdaptiveOnsetDetector.Detect(
                frames,
                hopSeconds,
                detectionMode);
            BeatGridData beatGrid = TempoBeatGridAnalyzer.Analyze(
                frames,
                candidates,
                durationSeconds,
                hopSeconds);
            List<SongSectionData> sections = SongSectionAnalyzer.Build(
                frames,
                candidates,
                beatGrid,
                durationSeconds,
                hopSeconds);

            RadialAudioAnalysisResult result = new RadialAudioAnalysisResult
            {
                analyzerVersion = 2,
                durationSeconds = durationSeconds,
                detectedBpm = beatGrid.bpm,
                tempoConfidence = beatGrid.tempoConfidence,
                beatGrid = beatGrid,
                featureFrames = frames,
                onsetCandidates = candidates,
                sections = sections
            };
            PopulateDurationAndConfidenceSummary(result);
            result.qualityReport = BuildQualityReport(result, hopSizeSamples);
            result.warnings.AddRange(result.qualityReport.warnings);
            return result;
        }

        public sealed class AnalysisJob
        {
            private readonly IAudioSampleSource source;
            private readonly AudioCandidateDetectionMode detectionMode;
            private readonly int hopSizeSamples;
            private readonly int chunkFrameCount;
            private readonly float[] interleavedBuffer;
            private readonly float[] monoRingBuffer;
            private readonly double[] hannWindow;
            private readonly double[] fftReal;
            private readonly double[] fftImaginary;
            private readonly double[] previousMagnitudes;
            private readonly double[] currentMagnitudes;
            private readonly Radix2Fft fft;
            private readonly List<AudioFeatureFrame> frames;
            private int ringWriteIndex;
            private long processedFrameCount;
            private long nextFeatureFrameEnd = AnalyzerV2Defaults.FftSize;
            private long offsetFrame;
            private RadialAudioAnalysisResult result;

            internal AnalysisJob(
                IAudioSampleSource source,
                AudioCandidateDetectionMode detectionMode)
            {
                this.source = source;
                this.detectionMode = detectionMode;
                hopSizeSamples = Math.Max(1, (int)Math.Round(source.SampleRate * 0.010d));
                int fftSize = AnalyzerV2Defaults.FftSize;
                chunkFrameCount = Math.Max(fftSize, source.SampleRate / 4);
                interleavedBuffer = new float[checked(chunkFrameCount * source.ChannelCount)];
                monoRingBuffer = new float[fftSize];
                hannWindow = new double[fftSize];
                fftReal = new double[fftSize];
                fftImaginary = new double[fftSize];
                previousMagnitudes = new double[(fftSize / 2) + 1];
                currentMagnitudes = new double[(fftSize / 2) + 1];
                fft = new Radix2Fft(fftSize);
                for (int i = 0; i < hannWindow.Length; i++)
                {
                    hannWindow[i] = 0.5d - (0.5d * Math.Cos(HannScale * i));
                }

                long estimatedFrameCount = source.FrameCount <= fftSize
                    ? 1L
                    : 1L + ((source.FrameCount - fftSize) / hopSizeSamples);
                frames = new List<AudioFeatureFrame>(
                    (int)Math.Min(estimatedFrameCount, 1000000L));
            }

            public bool FeatureExtractionComplete { get; private set; }

            public void StepFeatureExtraction(int maximumChunks)
            {
                if (maximumChunks <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(maximumChunks));
                }
                if (FeatureExtractionComplete)
                {
                    return;
                }

                int processedChunks = 0;
                while (offsetFrame < source.FrameCount && processedChunks < maximumChunks)
                {
                    int readableFrameCount = (int)Math.Min(
                        chunkFrameCount,
                        source.FrameCount - offsetFrame);
                    source.ReadFrames(offsetFrame, readableFrameCount, interleavedBuffer);
                    for (int frameIndex = 0; frameIndex < readableFrameCount; frameIndex++)
                    {
                        int sampleStart = frameIndex * source.ChannelCount;
                        double monoTotal = 0d;
                        for (int channelIndex = 0; channelIndex < source.ChannelCount; channelIndex++)
                        {
                            monoTotal += interleavedBuffer[sampleStart + channelIndex];
                        }

                        monoRingBuffer[ringWriteIndex] = (float)(monoTotal / source.ChannelCount);
                        ringWriteIndex = (ringWriteIndex + 1) % monoRingBuffer.Length;
                        processedFrameCount++;
                        if (processedFrameCount != nextFeatureFrameEnd)
                        {
                            continue;
                        }

                        frames.Add(ExtractFeatureFrame(
                            source.SampleRate,
                            processedFrameCount,
                            monoRingBuffer,
                            ringWriteIndex,
                            hannWindow,
                            fft,
                            fftReal,
                            fftImaginary,
                            previousMagnitudes,
                            currentMagnitudes));
                        nextFeatureFrameEnd += hopSizeSamples;
                    }

                    offsetFrame += readableFrameCount;
                    processedChunks++;
                }

                if (offsetFrame < source.FrameCount)
                {
                    return;
                }

                if (frames.Count == 0 && processedFrameCount > 0L)
                {
                    frames.Add(ExtractPartialFeatureFrame(
                        source.SampleRate,
                        processedFrameCount,
                        monoRingBuffer,
                        hannWindow,
                        fft,
                        fftReal,
                        fftImaginary,
                        previousMagnitudes,
                        currentMagnitudes));
                }
                FeatureExtractionComplete = true;
            }

            public RadialAudioAnalysisResult CompleteAnalysis()
            {
                if (!FeatureExtractionComplete)
                {
                    throw new InvalidOperationException("Feature extraction must complete before post-processing.");
                }
                if (result == null)
                {
                    result = BuildResult(
                        frames,
                        hopSizeSamples,
                        source.SampleRate,
                        source.FrameCount / (double)source.SampleRate,
                        detectionMode);
                }
                return result;
            }
        }

        private static AudioFeatureFrame ExtractFeatureFrame(
            int sampleRate,
            long frameEnd,
            float[] monoRingBuffer,
            int oldestSampleIndex,
            double[] hannWindow,
            Radix2Fft fft,
            double[] fftReal,
            double[] fftImaginary,
            double[] previousMagnitudes,
            double[] currentMagnitudes)
        {
            double squareTotal = 0d;
            for (int i = 0; i < fftReal.Length; i++)
            {
                double sample = monoRingBuffer[(oldestSampleIndex + i) % monoRingBuffer.Length];
                squareTotal += sample * sample;
                fftReal[i] = sample * hannWindow[i];
                fftImaginary[i] = 0d;
            }

            double timeSeconds = (frameEnd - (fftReal.Length * 0.5d)) / sampleRate;
            return TransformFeatureFrame(
                sampleRate,
                Math.Max(0d, timeSeconds),
                Math.Sqrt(squareTotal / fftReal.Length),
                fft,
                fftReal,
                fftImaginary,
                previousMagnitudes,
                currentMagnitudes);
        }

        private static AudioFeatureFrame ExtractPartialFeatureFrame(
            int sampleRate,
            long availableFrameCount,
            float[] monoSamples,
            double[] hannWindow,
            Radix2Fft fft,
            double[] fftReal,
            double[] fftImaginary,
            double[] previousMagnitudes,
            double[] currentMagnitudes)
        {
            int available = (int)Math.Min(availableFrameCount, monoSamples.Length);
            double squareTotal = 0d;
            for (int i = 0; i < fftReal.Length; i++)
            {
                double sample = i < available ? monoSamples[i] : 0d;
                squareTotal += sample * sample;
                fftReal[i] = sample * hannWindow[i];
                fftImaginary[i] = 0d;
            }

            return TransformFeatureFrame(
                sampleRate,
                availableFrameCount / (2d * sampleRate),
                Math.Sqrt(squareTotal / Math.Max(1, available)),
                fft,
                fftReal,
                fftImaginary,
                previousMagnitudes,
                currentMagnitudes);
        }

        private static AudioFeatureFrame TransformFeatureFrame(
            int sampleRate,
            double timeSeconds,
            double rms,
            Radix2Fft fft,
            double[] fftReal,
            double[] fftImaginary,
            double[] previousMagnitudes,
            double[] currentMagnitudes)
        {
            fft.Transform(fftReal, fftImaginary);
            double lowEnergy = 0d;
            double midEnergy = 0d;
            double highEnergy = 0d;
            double lowFlux = 0d;
            double midFlux = 0d;
            double highFlux = 0d;
            int lowBinCount = 0;
            int midBinCount = 0;
            int highBinCount = 0;

            for (int bin = 1; bin < currentMagnitudes.Length; bin++)
            {
                double frequency = bin * sampleRate / (double)fftReal.Length;
                double magnitude = Math.Sqrt(
                    (fftReal[bin] * fftReal[bin])
                    + (fftImaginary[bin] * fftImaginary[bin])) / fftReal.Length;
                currentMagnitudes[bin] = magnitude;
                double positiveFlux = Math.Max(0d, magnitude - previousMagnitudes[bin]);
                double power = magnitude * magnitude;

                if (frequency >= LowMinimumHz && frequency < LowMaximumHz)
                {
                    lowEnergy += power;
                    lowFlux += positiveFlux;
                    lowBinCount++;
                }
                else if (frequency >= LowMaximumHz && frequency < MidMaximumHz)
                {
                    midEnergy += power;
                    midFlux += positiveFlux;
                    midBinCount++;
                }
                else if (frequency >= MidMaximumHz && frequency <= HighMaximumHz)
                {
                    highEnergy += power;
                    highFlux += positiveFlux;
                    highBinCount++;
                }

                previousMagnitudes[bin] = magnitude;
            }

            lowEnergy = Average(lowEnergy, lowBinCount);
            midEnergy = Average(midEnergy, midBinCount);
            highEnergy = Average(highEnergy, highBinCount);
            lowFlux = Average(lowFlux, lowBinCount);
            midFlux = Average(midFlux, midBinCount);
            highFlux = Average(highFlux, highBinCount);

            return new AudioFeatureFrame
            {
                timeSeconds = timeSeconds,
                rms = rms,
                lowLogEnergy = Math.Log10(1d + (lowEnergy * 1000000d)),
                midLogEnergy = Math.Log10(1d + (midEnergy * 1000000d)),
                highLogEnergy = Math.Log10(1d + (highEnergy * 1000000d)),
                lowFlux = lowFlux,
                midFlux = midFlux,
                highFlux = highFlux,
                spectralFlux = lowFlux + midFlux + highFlux
            };
        }

        private static void PopulateDurationAndConfidenceSummary(RadialAudioAnalysisResult result)
        {
            int silentFrameCount = 0;
            for (int i = 0; i < result.featureFrames.Count; i++)
            {
                if (result.featureFrames[i].isSilent)
                {
                    silentFrameCount++;
                }
            }

            result.silentDurationSeconds = result.featureFrames.Count == 0
                ? result.durationSeconds
                : result.durationSeconds * silentFrameCount / result.featureFrames.Count;
            result.activeDurationSeconds = Math.Max(
                0d,
                result.durationSeconds - result.silentDurationSeconds);

            if (result.onsetCandidates.Count == 0)
            {
                return;
            }

            result.minimumCandidateConfidence = 1d;
            double confidenceTotal = 0d;
            for (int i = 0; i < result.onsetCandidates.Count; i++)
            {
                double confidence = result.onsetCandidates[i].confidence;
                result.minimumCandidateConfidence = Math.Min(
                    result.minimumCandidateConfidence,
                    confidence);
                result.maximumCandidateConfidence = Math.Max(
                    result.maximumCandidateConfidence,
                    confidence);
                confidenceTotal += confidence;
            }

            result.averageCandidateConfidence = confidenceTotal / result.onsetCandidates.Count;
        }

        private static AnalyzerQualityReport BuildQualityReport(
            RadialAudioAnalysisResult result,
            int hopSizeSamples)
        {
            AnalyzerQualityReport report = new AnalyzerQualityReport
            {
                frameCount = result.featureFrames.Count,
                candidateCount = result.onsetCandidates.Count,
                activeDurationSeconds = result.activeDurationSeconds,
                silentDurationSeconds = result.silentDurationSeconds,
                detectedBpm = result.detectedBpm,
                tempoConfidence = result.tempoConfidence,
                sectionCount = result.sections.Count,
                minimumCandidateConfidence = result.minimumCandidateConfidence,
                averageCandidateConfidence = result.averageCandidateConfidence,
                maximumCandidateConfidence = result.maximumCandidateConfidence,
                adaptiveOnsetCandidateCount = result.onsetCandidates.Count,
                gridCandidateCount = result.beatGrid.beatTimesSeconds.Count
                    + result.beatGrid.subdivisionTimesSeconds.Count,
                fftSize = AnalyzerV2Defaults.FftSize,
                hopSizeSamples = hopSizeSamples
            };

            double maximumRms = 0d;
            for (int i = 0; i < result.featureFrames.Count; i++)
            {
                maximumRms = Math.Max(maximumRms, result.featureFrames[i].rms);
            }

            for (int i = 0; i < result.onsetCandidates.Count; i++)
            {
                OnsetCandidateData candidate = result.onsetCandidates[i];
                if ((candidate.supportingBands & AudioBandMask.Low) != 0)
                {
                    report.lowBandCandidateCount++;
                }

                if ((candidate.supportingBands & AudioBandMask.Mid) != 0)
                {
                    report.midBandCandidateCount++;
                }

                if ((candidate.supportingBands & AudioBandMask.High) != 0)
                {
                    report.highBandCandidateCount++;
                }

                if (candidate.confidence < 0.40d)
                {
                    report.lowConfidenceCandidateCount++;
                }
                else if (candidate.confidence < 0.70d)
                {
                    report.mediumConfidenceCandidateCount++;
                }
                else
                {
                    report.highConfidenceCandidateCount++;
                }
            }

            report.longestActiveCandidateGapSeconds = ComputeLongestActiveCandidateGap(
                result.sections,
                result.onsetCandidates);

            double activeCandidateRate = result.activeDurationSeconds <= 0d
                ? 0d
                : result.onsetCandidates.Count / result.activeDurationSeconds;
            if (maximumRms < 0.001d
                || result.activeDurationSeconds < Math.Min(0.75d, result.durationSeconds * 0.20d))
            {
                report.warnings.Add("Insufficient Signal");
            }

            if (result.tempoConfidence < 0.35d)
            {
                report.warnings.Add("Tempo Uncertain");
            }

            if (result.activeDurationSeconds > 0d
                && (activeCandidateRate < 0.35d
                    || report.longestActiveCandidateGapSeconds > 4d))
            {
                report.warnings.Add("Sparse Active Sections");
            }

            return report;
        }

        private static double ComputeLongestActiveCandidateGap(
            IReadOnlyList<SongSectionData> sections,
            IReadOnlyList<OnsetCandidateData> candidates)
        {
            double longestGap = 0d;
            int candidateIndex = 0;
            for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
            {
                SongSectionData section = sections[sectionIndex];
                if (section.activityLevel == SongSectionActivityLevel.Silent)
                {
                    continue;
                }

                while (candidateIndex < candidates.Count
                    && candidates[candidateIndex].timeSeconds < section.startTimeSeconds)
                {
                    candidateIndex++;
                }

                double previousTime = section.startTimeSeconds;
                int localCandidateIndex = candidateIndex;
                while (localCandidateIndex < candidates.Count
                    && candidates[localCandidateIndex].timeSeconds < section.endTimeSeconds)
                {
                    double candidateTime = candidates[localCandidateIndex].timeSeconds;
                    longestGap = Math.Max(longestGap, candidateTime - previousTime);
                    previousTime = candidateTime;
                    localCandidateIndex++;
                }

                longestGap = Math.Max(longestGap, section.endTimeSeconds - previousTime);
                candidateIndex = localCandidateIndex;
            }

            return longestGap;
        }

        private static double Average(double total, int count)
        {
            return count <= 0 ? 0d : total / count;
        }

        private static void ValidateSource(IAudioSampleSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (source.SampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(source), "Sample rate must be positive.");
            }

            if (source.ChannelCount <= 0 || source.ChannelCount > 32)
            {
                throw new ArgumentOutOfRangeException(nameof(source), "Channel count must be between 1 and 32.");
            }

            if (source.FrameCount <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(source), "Audio source must contain samples.");
            }
        }
    }
}
