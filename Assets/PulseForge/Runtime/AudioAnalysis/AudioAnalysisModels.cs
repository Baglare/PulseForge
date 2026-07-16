using System;
using System.Collections.Generic;

namespace PulseForge.AudioAnalysis
{
    [Flags]
    public enum AudioBandMask
    {
        None = 0,
        Low = 1 << 0,
        Mid = 1 << 1,
        High = 1 << 2
    }

    public enum SongSectionActivityLevel
    {
        Silent,
        Low,
        Active,
        Peak
    }

    [Serializable]
    public sealed class RadialAudioAnalysisResult
    {
        public int analyzerVersion = 2;
        public double durationSeconds;
        public double detectedBpm;
        public double tempoConfidence;
        public BeatGridData beatGrid = new BeatGridData();
        public List<AudioFeatureFrame> featureFrames = new List<AudioFeatureFrame>();
        public List<OnsetCandidateData> onsetCandidates = new List<OnsetCandidateData>();
        public List<SongSectionData> sections = new List<SongSectionData>();
        public double activeDurationSeconds;
        public double silentDurationSeconds;
        public double minimumCandidateConfidence;
        public double averageCandidateConfidence;
        public double maximumCandidateConfidence;
        public AnalyzerQualityReport qualityReport = new AnalyzerQualityReport();
        public List<string> warnings = new List<string>();
    }

    [Serializable]
    public sealed class AudioFeatureFrame
    {
        public double timeSeconds;
        public double rms;
        public double lowLogEnergy;
        public double midLogEnergy;
        public double highLogEnergy;
        public double spectralFlux;
        public double lowFlux;
        public double midFlux;
        public double highFlux;
        public double localContrast;
        public double localNoiseFloor;
        public double lowOnsetThreshold;
        public double midOnsetThreshold;
        public double highOnsetThreshold;
        public bool isSilent;
    }

    [Serializable]
    public sealed class OnsetCandidateData
    {
        public double timeSeconds;
        public double confidence;
        public double strength;
        public double localContrast;
        public double beatAlignment;
        public AudioBandMask supportingBands;
        public double lowStrength;
        public double midStrength;
        public double highStrength;
        public bool selectedByAdaptiveThreshold = true;
    }

    [Serializable]
    public sealed class BeatGridData
    {
        public double bpm;
        public double tempoConfidence;
        public double beatIntervalSeconds;
        public double phaseSeconds;
        public double gridStrength;
        public double gridConfidence;
        public List<double> beatTimesSeconds = new List<double>();
        public List<double> subdivisionTimesSeconds = new List<double>();
    }

    [Serializable]
    public sealed class SongSectionData
    {
        public double startTimeSeconds;
        public double endTimeSeconds;
        public SongSectionActivityLevel activityLevel;
        public double activity;
        public double averageRms;
        public double averageLowLogEnergy;
        public double averageMidLogEnergy;
        public double averageHighLogEnergy;
        public int candidateCount;
        public double tempoConfidence;
        public double gridConfidence;
    }

    [Serializable]
    public sealed class AnalyzerQualityReport
    {
        public int frameCount;
        public int candidateCount;
        public int lowBandCandidateCount;
        public int midBandCandidateCount;
        public int highBandCandidateCount;
        public double activeDurationSeconds;
        public double silentDurationSeconds;
        public double detectedBpm;
        public double tempoConfidence;
        public int sectionCount;
        public int lowConfidenceCandidateCount;
        public int mediumConfidenceCandidateCount;
        public int highConfidenceCandidateCount;
        public double minimumCandidateConfidence;
        public double averageCandidateConfidence;
        public double maximumCandidateConfidence;
        public double longestActiveCandidateGapSeconds;
        public int adaptiveOnsetCandidateCount;
        public int gridCandidateCount;
        public int fftSize;
        public int hopSizeSamples;
        public List<string> warnings = new List<string>();
    }
}
