using ELOR.VKAPILib.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;

namespace ELOR.Laney.Core {
    public static class LocalVoiceTranscriptionService {
        private static readonly SemaphoreSlim SyncRoot = new SemaphoreSlim(1, 1);
        private static Dictionary<string, string> cache;

        public static bool CanTranscribe => Settings.LocalVoiceTranscriptionEnabled && File.Exists(GetModelPath());

        public static string GetTranscript(AudioMessage audioMessage) {
            if (!String.IsNullOrWhiteSpace(audioMessage?.Transcript)) return audioMessage.Transcript.Trim();
            if (audioMessage == null) return null;

            Dictionary<string, string> transcripts = LoadCache();
            return transcripts.TryGetValue(GetAudioMessageKey(audioMessage), out string transcript) ? transcript : null;
        }

        public static async Task<string> TryTranscribeRecordedWavAsync(string path) {
            if (!Settings.LocalVoiceTranscriptionEnabled || String.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

            string modelPath = GetModelPath();
            if (!File.Exists(modelPath)) {
                Log.Information("Local voice transcription skipped: Whisper model is not configured.");
                return null;
            }

            try {
                using WhisperFactory factory = WhisperFactory.FromPath(modelPath, new WhisperFactoryOptions {
                    UseGpu = false
                });
                using WhisperProcessor processor = BuildProcessor(factory);
                await using FileStream stream = File.OpenRead(path);

                StringBuilder text = new StringBuilder();
                await foreach (SegmentData segment in processor.ProcessAsync(stream)) {
                    string segmentText = segment.Text?.Trim();
                    if (!String.IsNullOrWhiteSpace(segmentText)) {
                        if (text.Length > 0) text.Append(' ');
                        text.Append(segmentText);
                    }
                }

                return NormalizeTranscript(text.ToString());
            } catch (Exception ex) {
                Log.Warning(ex, "Local voice transcription failed for {Path}.", path);
                return null;
            }
        }

        public static async Task SaveTranscriptAsync(AudioMessage audioMessage, string transcript) {
            if (audioMessage == null || String.IsNullOrWhiteSpace(transcript)) return;

            await SyncRoot.WaitAsync();
            try {
                Dictionary<string, string> transcripts = LoadCache();
                transcripts[GetAudioMessageKey(audioMessage)] = NormalizeTranscript(transcript);
                string directory = Path.GetDirectoryName(GetCachePath());
                Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(GetCachePath(), JsonSerializer.Serialize(transcripts), Encoding.UTF8);
            } finally {
                SyncRoot.Release();
            }
        }

        private static WhisperProcessor BuildProcessor(WhisperFactory factory) {
            WhisperProcessorBuilder builder = factory.CreateBuilder()
                .WithThreads(Math.Clamp(Environment.ProcessorCount / 2, 1, 4))
                .WithNoContext()
                .WithTemperature(0.0f)
                .WithNoSpeechThreshold(0.45f);

            string language = Settings.LocalVoiceTranscriptionLanguage;
            if (String.Equals(language, "auto", StringComparison.OrdinalIgnoreCase) || String.IsNullOrWhiteSpace(language)) {
                builder = builder.WithLanguageDetection();
            } else {
                builder = builder.WithLanguage(language);
            }

            return builder.Build();
        }

        private static Dictionary<string, string> LoadCache() {
            if (cache != null) return cache;

            string path = GetCachePath();
            if (!File.Exists(path)) {
                cache = new Dictionary<string, string>();
                return cache;
            }

            try {
                cache = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path, Encoding.UTF8)) ?? new Dictionary<string, string>();
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot read local voice transcript cache.");
                cache = new Dictionary<string, string>();
            }

            return cache;
        }

        private static string GetModelPath() {
            string configured = Settings.LocalVoiceTranscriptionModelPath;
            if (!String.IsNullOrWhiteSpace(configured)) return configured;

            string directory = Path.Combine(App.LocalDataPath, "whisper");
            if (!Directory.Exists(directory)) return Path.Combine(directory, "ggml-base.bin");

            return Directory.EnumerateFiles(directory, "*.bin", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path.Contains("tiny", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(path => path)
                .FirstOrDefault() ?? Path.Combine(directory, "ggml-base.bin");
        }

        private static string GetCachePath() {
            return LocalDataProfile.GetCurrentAccountPath("voice", "transcripts.json");
        }

        private static string GetAudioMessageKey(AudioMessage audioMessage) {
            if (audioMessage.OwnerId != 0 && audioMessage.Id != 0) return $"{audioMessage.OwnerId}_{audioMessage.Id}";

            string source = audioMessage.Link ?? audioMessage.ToString();
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source ?? String.Empty))).ToLowerInvariant();
        }

        private static string NormalizeTranscript(string text) {
            if (String.IsNullOrWhiteSpace(text)) return null;
            return String.Join(' ', text.Split(['\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Trim();
        }
    }
}
