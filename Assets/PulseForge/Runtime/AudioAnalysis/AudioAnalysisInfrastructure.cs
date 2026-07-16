using System;

namespace PulseForge.AudioAnalysis
{
    public enum AudioCandidateDetectionMode
    {
        Onset,
        Amplitude
    }

    public interface IAudioSampleSource
    {
        int SampleRate { get; }

        int ChannelCount { get; }

        long FrameCount { get; }

        void ReadFrames(long startFrame, int frameCount, float[] interleavedBuffer);
    }

    public static class AnalyzerV2Defaults
    {
        public const int FftSize = 2048;
        public const int PreferredHopSizeSamples = 441;
        public const double LocalWindowSeconds = 2d;
        public const double CandidateMergeSeconds = 0.060d;
        public const double MinimumSilentRegionSeconds = 0.750d;
        public const double MinimumTempoBpm = 70d;
        public const double MaximumTempoBpm = 180d;
    }

    internal sealed class Radix2Fft
    {
        private readonly int size;
        private readonly int[] bitReversed;
        private readonly double[] cosine;
        private readonly double[] sine;

        public Radix2Fft(int size)
        {
            if (size < 2 || (size & (size - 1)) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "FFT size must be a power of two.");
            }

            this.size = size;
            bitReversed = new int[size];
            cosine = new double[size / 2];
            sine = new double[size / 2];

            int bitCount = 0;
            for (int value = size; value > 1; value >>= 1)
            {
                bitCount++;
            }

            for (int i = 0; i < size; i++)
            {
                int reversed = 0;
                int value = i;
                for (int bit = 0; bit < bitCount; bit++)
                {
                    reversed = (reversed << 1) | (value & 1);
                    value >>= 1;
                }

                bitReversed[i] = reversed;
            }

            for (int i = 0; i < cosine.Length; i++)
            {
                double angle = -2d * Math.PI * i / size;
                cosine[i] = Math.Cos(angle);
                sine[i] = Math.Sin(angle);
            }
        }

        public void Transform(double[] real, double[] imaginary)
        {
            if (real == null || imaginary == null || real.Length != size || imaginary.Length != size)
            {
                throw new ArgumentException("FFT buffers must match the configured size.");
            }

            for (int i = 0; i < size; i++)
            {
                int reversed = bitReversed[i];
                if (reversed <= i)
                {
                    continue;
                }

                double realValue = real[i];
                real[i] = real[reversed];
                real[reversed] = realValue;
                double imaginaryValue = imaginary[i];
                imaginary[i] = imaginary[reversed];
                imaginary[reversed] = imaginaryValue;
            }

            for (int length = 2; length <= size; length <<= 1)
            {
                int halfLength = length >> 1;
                int tableStep = size / length;
                for (int start = 0; start < size; start += length)
                {
                    for (int offset = 0; offset < halfLength; offset++)
                    {
                        int tableIndex = offset * tableStep;
                        int evenIndex = start + offset;
                        int oddIndex = evenIndex + halfLength;
                        double oddReal = real[oddIndex] * cosine[tableIndex]
                            - imaginary[oddIndex] * sine[tableIndex];
                        double oddImaginary = real[oddIndex] * sine[tableIndex]
                            + imaginary[oddIndex] * cosine[tableIndex];
                        double evenReal = real[evenIndex];
                        double evenImaginary = imaginary[evenIndex];
                        real[evenIndex] = evenReal + oddReal;
                        imaginary[evenIndex] = evenImaginary + oddImaginary;
                        real[oddIndex] = evenReal - oddReal;
                        imaginary[oddIndex] = evenImaginary - oddImaginary;
                    }
                }
            }
        }
    }

    internal static class AnalyzerV2Math
    {
        public static double Clamp01(double value)
        {
            if (value <= 0d)
            {
                return 0d;
            }

            return value >= 1d ? 1d : value;
        }

        public static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        public static void MedianAndMad(double[] values, double[] deviations, int count, out double median, out double mad)
        {
            if (count <= 0)
            {
                median = 0d;
                mad = 0d;
                return;
            }

            Array.Sort(values, 0, count);
            median = MedianOfSorted(values, count);
            for (int i = 0; i < count; i++)
            {
                deviations[i] = Math.Abs(values[i] - median);
            }

            Array.Sort(deviations, 0, count);
            mad = MedianOfSorted(deviations, count);
        }

        public static double Percentile(double[] values, int count, double percentile)
        {
            if (count <= 0)
            {
                return 0d;
            }

            Array.Sort(values, 0, count);
            double position = Clamp01(percentile) * (count - 1);
            int lower = (int)Math.Floor(position);
            int upper = (int)Math.Ceiling(position);
            if (lower == upper)
            {
                return values[lower];
            }

            double fraction = position - lower;
            return values[lower] + ((values[upper] - values[lower]) * fraction);
        }

        private static double MedianOfSorted(double[] values, int count)
        {
            int middle = count / 2;
            return (count & 1) == 1
                ? values[middle]
                : (values[middle - 1] + values[middle]) * 0.5d;
        }
    }
}
