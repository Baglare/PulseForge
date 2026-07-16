using System;
using System.Linq;
using NUnit.Framework;
using PulseForge.AudioAnalysis;

namespace PulseForge.Tests.EditMode
{
    public sealed class RadialAudioAnalyzerV2Tests
    {
        private const int SampleRate = 44100;

        [Test]
        public void AdaptiveOnsetPreservesQuietActiveClicksAfterLoudSection()
        {
            float[] samples = new float[SampleRate * 6];
            AddClick(samples, 0.5d, 1f);
            AddClick(samples, 1.0d, 1f);
            AddClick(samples, 1.5d, 1f);
            AddClick(samples, 3.0d, 0.08f);
            AddClick(samples, 3.5d, 0.08f);
            AddClick(samples, 4.0d, 0.08f);
            AddClick(samples, 4.5d, 0.08f);

            RadialAudioAnalysisResult result = Analyze(samples);

            int quietClickMatches = result.onsetCandidates.Count(candidate =>
                IsNear(candidate.timeSeconds, 3.0d)
                || IsNear(candidate.timeSeconds, 3.5d)
                || IsNear(candidate.timeSeconds, 4.0d)
                || IsNear(candidate.timeSeconds, 4.5d));
            Assert.That(quietClickMatches, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void ClickTrackAt120BpmProducesTempoAndBeatGrid()
        {
            float[] samples = CreateClickTrack(10d, 0.5d);

            RadialAudioAnalysisResult result = Analyze(samples);

            Assert.That(result.detectedBpm, Is.EqualTo(120d).Within(2d));
            Assert.That(result.tempoConfidence, Is.GreaterThan(0.40d));
            Assert.That(result.beatGrid.beatTimesSeconds, Has.Count.GreaterThan(10));
            Assert.That(result.beatGrid.subdivisionTimesSeconds, Is.Not.Empty);
            Assert.That(result.sections, Is.Not.Empty);
            Assert.That(result.qualityReport.gridCandidateCount, Is.GreaterThan(10));
        }

        [Test]
        public void SustainedSilenceIsReportedWithoutForcedCandidates()
        {
            float[] samples = new float[SampleRate * 5];

            RadialAudioAnalysisResult result = Analyze(samples);

            Assert.That(result.silentDurationSeconds, Is.GreaterThan(4d));
            Assert.That(result.onsetCandidates, Is.Empty);
            Assert.That(result.qualityReport.warnings, Does.Contain("Insufficient Signal"));
            Assert.That(result.qualityReport.warnings, Does.Contain("Tempo Uncertain"));
        }

        [Test]
        public void FrequencyBandsSeparateLowMidAndHighTones()
        {
            RadialAudioAnalysisResult low = Analyze(CreateTone(100d));
            RadialAudioAnalysisResult mid = Analyze(CreateTone(1000d));
            RadialAudioAnalysisResult high = Analyze(CreateTone(5000d));

            Assert.That(Average(low, Band.Low), Is.GreaterThan(Average(low, Band.Mid)));
            Assert.That(Average(low, Band.Low), Is.GreaterThan(Average(low, Band.High)));
            Assert.That(Average(mid, Band.Mid), Is.GreaterThan(Average(mid, Band.Low)));
            Assert.That(Average(mid, Band.Mid), Is.GreaterThan(Average(mid, Band.High)));
            Assert.That(Average(high, Band.High), Is.GreaterThan(Average(high, Band.Low)));
            Assert.That(Average(high, Band.High), Is.GreaterThan(Average(high, Band.Mid)));
        }

        [Test]
        public void BandPeaksWithinSixtyMillisecondsMergeIntoOneCandidate()
        {
            float[] samples = new float[SampleRate * 3];
            AddSineBurst(samples, 1.00d, 0.025d, 100d, 0.8f);
            AddSineBurst(samples, 1.04d, 0.025d, 5000d, 0.8f);

            RadialAudioAnalysisResult result = Analyze(samples);

            OnsetCandidateData[] mergedWindow = result.onsetCandidates
                .Where(candidate => candidate.timeSeconds >= 0.90d && candidate.timeSeconds <= 1.20d)
                .ToArray();
            Assert.That(mergedWindow, Has.Length.EqualTo(1));
            Assert.That(
                mergedWindow[0].supportingBands & (AudioBandMask.Low | AudioBandMask.High),
                Is.EqualTo(AudioBandMask.Low | AudioBandMask.High));
        }

        [Test]
        public void SameInputProducesDeterministicAnalysis()
        {
            float[] samples = CreateClickTrack(6d, 0.5d);

            RadialAudioAnalysisResult first = Analyze(samples);
            RadialAudioAnalysisResult second = Analyze(samples);

            Assert.That(second.detectedBpm, Is.EqualTo(first.detectedBpm).Within(0.000000000001d));
            Assert.That(second.tempoConfidence, Is.EqualTo(first.tempoConfidence).Within(0.000000000001d));
            Assert.That(second.onsetCandidates, Has.Count.EqualTo(first.onsetCandidates.Count));
            for (int i = 0; i < first.onsetCandidates.Count; i++)
            {
                Assert.That(
                    second.onsetCandidates[i].timeSeconds,
                    Is.EqualTo(first.onsetCandidates[i].timeSeconds).Within(0.000000000001d));
                Assert.That(
                    second.onsetCandidates[i].confidence,
                    Is.EqualTo(first.onsetCandidates[i].confidence).Within(0.000000000001d));
            }
        }

        [Test]
        public void LongSourceUsesBoundedReusableReadBufferAndFrameSummaries()
        {
            ProceduralClickSource source = new ProceduralClickSource(
                SampleRate,
                2,
                SampleRate * 25L,
                SampleRate / 2);

            RadialAudioAnalysisResult result = RadialAudioAnalyzerV2.Analyze(source);

            Assert.That(source.MaximumRequestedFrameCount, Is.LessThanOrEqualTo(SampleRate));
            Assert.That(source.ReadCallCount, Is.GreaterThan(1));
            Assert.That(source.BufferInstanceChanged, Is.False);
            Assert.That(result.qualityReport.frameCount, Is.LessThan(source.FrameCount / 100L));
        }

        private static RadialAudioAnalysisResult Analyze(float[] samples)
        {
            return RadialAudioAnalyzerV2.Analyze(new ArrayAudioSampleSource(samples, SampleRate, 1));
        }

        private static float[] CreateClickTrack(double durationSeconds, double intervalSeconds)
        {
            float[] samples = new float[(int)Math.Round(durationSeconds * SampleRate)];
            for (double time = 0.5d; time < durationSeconds - 0.1d; time += intervalSeconds)
            {
                AddClick(samples, time, 1f);
            }

            return samples;
        }

        private static float[] CreateTone(double frequency)
        {
            float[] samples = new float[SampleRate * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = (float)(Math.Sin(2d * Math.PI * frequency * i / SampleRate) * 0.5d);
            }

            return samples;
        }

        private static void AddClick(float[] samples, double timeSeconds, float amplitude)
        {
            int start = (int)Math.Round(timeSeconds * SampleRate);
            const int clickLength = 128;
            for (int i = 0; i < clickLength && start + i < samples.Length; i++)
            {
                samples[start + i] += amplitude * (1f - (i / (float)clickLength));
            }
        }

        private static void AddSineBurst(
            float[] samples,
            double startSeconds,
            double durationSeconds,
            double frequency,
            float amplitude)
        {
            int start = (int)Math.Round(startSeconds * SampleRate);
            int length = (int)Math.Round(durationSeconds * SampleRate);
            for (int i = 0; i < length && start + i < samples.Length; i++)
            {
                double envelope = Math.Sin(Math.PI * i / Math.Max(1, length - 1));
                samples[start + i] += (float)(
                    Math.Sin(2d * Math.PI * frequency * i / SampleRate)
                    * envelope
                    * amplitude);
            }
        }

        private static bool IsNear(double actual, double expected)
        {
            return Math.Abs(actual - expected) <= 0.08d;
        }

        private static double Average(RadialAudioAnalysisResult result, Band band)
        {
            return result.featureFrames.Average(frame =>
            {
                switch (band)
                {
                    case Band.Low:
                        return frame.lowLogEnergy;
                    case Band.Mid:
                        return frame.midLogEnergy;
                    default:
                        return frame.highLogEnergy;
                }
            });
        }

        private enum Band
        {
            Low,
            Mid,
            High
        }

        private sealed class ArrayAudioSampleSource : IAudioSampleSource
        {
            private readonly float[] samples;

            public ArrayAudioSampleSource(float[] samples, int sampleRate, int channelCount)
            {
                this.samples = samples ?? throw new ArgumentNullException(nameof(samples));
                SampleRate = sampleRate;
                ChannelCount = channelCount;
            }

            public int SampleRate { get; }

            public int ChannelCount { get; }

            public long FrameCount => samples.Length / ChannelCount;

            public void ReadFrames(long startFrame, int frameCount, float[] interleavedBuffer)
            {
                Array.Copy(
                    samples,
                    checked((int)(startFrame * ChannelCount)),
                    interleavedBuffer,
                    0,
                    checked(frameCount * ChannelCount));
            }
        }

        private sealed class ProceduralClickSource : IAudioSampleSource
        {
            private readonly int beatIntervalFrames;
            private float[] firstBuffer;

            public ProceduralClickSource(
                int sampleRate,
                int channelCount,
                long frameCount,
                int beatIntervalFrames)
            {
                SampleRate = sampleRate;
                ChannelCount = channelCount;
                FrameCount = frameCount;
                this.beatIntervalFrames = beatIntervalFrames;
            }

            public int SampleRate { get; }

            public int ChannelCount { get; }

            public long FrameCount { get; }

            public int MaximumRequestedFrameCount { get; private set; }

            public int ReadCallCount { get; private set; }

            public bool BufferInstanceChanged { get; private set; }

            public void ReadFrames(long startFrame, int frameCount, float[] interleavedBuffer)
            {
                ReadCallCount++;
                MaximumRequestedFrameCount = Math.Max(MaximumRequestedFrameCount, frameCount);
                if (firstBuffer == null)
                {
                    firstBuffer = interleavedBuffer;
                }
                else if (!ReferenceEquals(firstBuffer, interleavedBuffer))
                {
                    BufferInstanceChanged = true;
                }

                int sampleCount = frameCount * ChannelCount;
                Array.Clear(interleavedBuffer, 0, sampleCount);
                for (int localFrame = 0; localFrame < frameCount; localFrame++)
                {
                    long absoluteFrame = startFrame + localFrame;
                    int beatOffset = (int)(absoluteFrame % beatIntervalFrames);
                    if (beatOffset >= 128)
                    {
                        continue;
                    }

                    float amplitude = 1f - (beatOffset / 128f);
                    int sampleStart = localFrame * ChannelCount;
                    for (int channel = 0; channel < ChannelCount; channel++)
                    {
                        interleavedBuffer[sampleStart + channel] = amplitude;
                    }
                }
            }
        }
    }
}
