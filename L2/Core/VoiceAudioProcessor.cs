using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ELOR.Laney.Core {
    public sealed class VoiceProcessingResult {
        public string OutputPath { get; set; }
        public double PeakBefore { get; set; }
        public double PeakAfter { get; set; }
        public double RmsBefore { get; set; }
        public double RmsAfter { get; set; }
        public double NoiseGateThreshold { get; set; }
        public double GainDb { get; set; }
    }

    public static class VoiceAudioProcessor {
        private const double TargetRms = 0.12;
        private const double MaxGain = 4.0;
        private const double PeakLimit = 0.96;
        private const double GateFloor = 0.004;
        private const double GateAttenuation = 0.18;
        private const double HighPassCutoffHz = 90.0;

        public static VoiceProcessingResult ProcessRecordedWav(string inputPath) {
            if (String.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath)) return null;

            try {
                PcmWav wav = ReadPcm16MonoWav(inputPath);
                if (wav == null || wav.Samples.Length == 0) return null;

                double[] samples = wav.Samples.Select(s => s / 32768.0).ToArray();
                double rmsBefore = CalculateRms(samples);
                double peakBefore = CalculatePeak(samples);

                ApplyHighPass(samples, wav.SampleRate);
                double threshold = Math.Max(GateFloor, EstimateNoiseRms(samples, wav.SampleRate) * 1.8);
                ApplyNoiseGate(samples, threshold);

                double rmsAfterGate = CalculateRms(samples);
                double peakAfterGate = CalculatePeak(samples);
                double gain = rmsAfterGate > 0 ? Math.Min(MaxGain, TargetRms / rmsAfterGate) : 1.0;
                if (peakAfterGate * gain > PeakLimit) gain = PeakLimit / peakAfterGate;
                if (Double.IsNaN(gain) || Double.IsInfinity(gain) || gain <= 0) gain = 1.0;

                ApplyGainAndLimiter(samples, gain);
                string outputPath = Path.Combine(
                    Path.GetDirectoryName(inputPath),
                    $"{Path.GetFileNameWithoutExtension(inputPath)}-processed.wav");
                WritePcm16MonoWav(outputPath, wav.SampleRate, samples);

                return new VoiceProcessingResult {
                    OutputPath = outputPath,
                    PeakBefore = peakBefore,
                    PeakAfter = CalculatePeak(samples),
                    RmsBefore = rmsBefore,
                    RmsAfter = CalculateRms(samples),
                    NoiseGateThreshold = threshold,
                    GainDb = 20.0 * Math.Log10(gain)
                };
            } catch (Exception ex) {
                Log.Warning(ex, "Voice processing failed for {Path}. Original file will be used.", inputPath);
                return null;
            }
        }

        private static PcmWav ReadPcm16MonoWav(string path) {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 44
                || Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF"
                || Encoding.ASCII.GetString(bytes, 8, 4) != "WAVE") {
                return null;
            }

            int offset = 12;
            short channels = 0;
            short bitsPerSample = 0;
            int sampleRate = 0;
            int dataOffset = -1;
            int dataLength = 0;

            while (offset + 8 <= bytes.Length) {
                string chunkId = Encoding.ASCII.GetString(bytes, offset, 4);
                int chunkLength = BitConverter.ToInt32(bytes, offset + 4);
                int chunkDataOffset = offset + 8;
                if (chunkLength < 0 || chunkDataOffset + chunkLength > bytes.Length) return null;

                if (chunkId == "fmt " && chunkLength >= 16) {
                    short formatTag = BitConverter.ToInt16(bytes, chunkDataOffset);
                    channels = BitConverter.ToInt16(bytes, chunkDataOffset + 2);
                    sampleRate = BitConverter.ToInt32(bytes, chunkDataOffset + 4);
                    bitsPerSample = BitConverter.ToInt16(bytes, chunkDataOffset + 14);
                    if (formatTag != 1) return null;
                } else if (chunkId == "data") {
                    dataOffset = chunkDataOffset;
                    dataLength = chunkLength;
                }

                offset = chunkDataOffset + chunkLength + (chunkLength % 2);
            }

            if (channels != 1 || bitsPerSample != 16 || sampleRate <= 0 || dataOffset < 0 || dataLength <= 0) return null;

            int sampleCount = dataLength / 2;
            short[] samples = new short[sampleCount];
            for (int i = 0; i < sampleCount; i++) {
                samples[i] = BitConverter.ToInt16(bytes, dataOffset + i * 2);
            }

            return new PcmWav {
                SampleRate = sampleRate,
                Samples = samples
            };
        }

        private static void WritePcm16MonoWav(string path, int sampleRate, double[] samples) {
            short[] pcm = new short[samples.Length];
            for (int i = 0; i < samples.Length; i++) {
                double clamped = Math.Clamp(samples[i], -1.0, 1.0);
                pcm[i] = (short)Math.Clamp((int)Math.Round(clamped * 32767.0), short.MinValue, short.MaxValue);
            }

            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            using BinaryWriter writer = new BinaryWriter(stream, Encoding.ASCII, true);
            int dataLength = pcm.Length * 2;
            short channels = 1;
            short bitsPerSample = 16;
            short blockAlign = (short)(channels * bitsPerSample / 8);

            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataLength);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * blockAlign);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataLength);
            foreach (short sample in pcm) writer.Write(sample);
        }

        private static void ApplyHighPass(double[] samples, int sampleRate) {
            if (samples.Length == 0 || sampleRate <= 0) return;

            double rc = 1.0 / (2.0 * Math.PI * HighPassCutoffHz);
            double dt = 1.0 / sampleRate;
            double alpha = rc / (rc + dt);
            double previousInput = samples[0];
            double previousOutput = samples[0];

            for (int i = 1; i < samples.Length; i++) {
                double input = samples[i];
                double output = alpha * (previousOutput + input - previousInput);
                samples[i] = output;
                previousInput = input;
                previousOutput = output;
            }
        }

        private static double EstimateNoiseRms(double[] samples, int sampleRate) {
            int count = Math.Clamp(sampleRate / 2, 1, samples.Length);
            if (count <= 0) return 0;

            double[] window = new double[count];
            Array.Copy(samples, window, count);
            Array.Sort(window, (a, b) => Math.Abs(a).CompareTo(Math.Abs(b)));
            int quietCount = Math.Max(1, count / 2);
            double sum = 0;
            for (int i = 0; i < quietCount; i++) sum += window[i] * window[i];
            return Math.Sqrt(sum / quietCount);
        }

        private static void ApplyNoiseGate(double[] samples, double threshold) {
            if (threshold <= 0) return;

            for (int i = 0; i < samples.Length; i++) {
                double amplitude = Math.Abs(samples[i]);
                if (amplitude < threshold) {
                    samples[i] *= GateAttenuation;
                } else if (amplitude < threshold * 2.0) {
                    double mix = (amplitude - threshold) / threshold;
                    samples[i] *= GateAttenuation + (1.0 - GateAttenuation) * mix;
                }
            }
        }

        private static void ApplyGainAndLimiter(double[] samples, double gain) {
            for (int i = 0; i < samples.Length; i++) {
                double value = samples[i] * gain;
                samples[i] = Math.Clamp(value, -PeakLimit, PeakLimit);
            }
        }

        private static double CalculateRms(IReadOnlyList<double> samples) {
            if (samples.Count == 0) return 0;

            double sum = 0;
            for (int i = 0; i < samples.Count; i++) sum += samples[i] * samples[i];
            return Math.Sqrt(sum / samples.Count);
        }

        private static double CalculatePeak(IReadOnlyList<double> samples) {
            double peak = 0;
            for (int i = 0; i < samples.Count; i++) peak = Math.Max(peak, Math.Abs(samples[i]));
            return peak;
        }

        private sealed class PcmWav {
            public int SampleRate { get; set; }
            public short[] Samples { get; set; }
        }
    }
}
