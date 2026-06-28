using ELOR.Laney.Core.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace ELOR.Laney.Core {
    public sealed class ApiDebugCallEntry {
        public DateTimeOffset StartedAt { get; set; }
        public string HttpMethod { get; set; }
        public string Method { get; set; }
        public string Uri { get; set; }
        public int? StatusCode { get; set; }
        public string StatusDescription { get; set; }
        public long DurationMs { get; set; }
        public string ErrorType { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();

        public string StartedAtText => StartedAt.ToLocalTime().ToString("HH:mm:ss.fff");
        public string DurationText => $"{DurationMs} ms";
        public string StatusText => ErrorType == null ? $"{(int?)StatusCode ?? 0}" : "ERR";
        public string ResultText => ErrorType == null ? $"{StatusCode} {StatusDescription}".Trim() : $"{ErrorType}: {ErrorMessage}";
        public string ParameterPreview => Parameters.Count == 0
            ? "без параметров"
            : String.Join("; ", Parameters.Take(6).Select(p => $"{p.Key}={p.Value}"));

        public string ToDetailsText() {
            string parameters = Parameters.Count == 0
                ? "  -"
                : String.Join(Environment.NewLine, Parameters.OrderBy(p => p.Key).Select(p => $"  {p.Key} = {p.Value}"));

            return $"Time: {StartedAt:O}{Environment.NewLine}"
                + $"Method: {Method}{Environment.NewLine}"
                + $"HTTP: {HttpMethod}{Environment.NewLine}"
                + $"URI: {Uri}{Environment.NewLine}"
                + $"Status: {ResultText}{Environment.NewLine}"
                + $"Duration: {DurationMs} ms{Environment.NewLine}"
                + $"Parameters:{Environment.NewLine}{parameters}";
        }
    }

    public static class ApiDebugMonitor {
        private const int MaxEntries = 300;
        private static readonly object SyncRoot = new object();
        private static readonly LinkedList<ApiDebugCallEntry> Entries = new LinkedList<ApiDebugCallEntry>();

        public static event EventHandler<ApiDebugCallEntry> EntryAdded;

        public static void Record(Uri uri, Dictionary<string, string> parameters, string httpMethod, HttpStatusCode? statusCode, string reasonPhrase, long durationMs, Exception exception = null) {
            if (!Settings.ApiDebugMonitorEnabled) return;
            if (!TryGetApiMethod(uri, out string method)) return;

            ApiDebugCallEntry entry = new ApiDebugCallEntry {
                StartedAt = DateTimeOffset.UtcNow.AddMilliseconds(-durationMs),
                HttpMethod = httpMethod,
                Method = method,
                Uri = SensitiveLogSanitizer.Sanitize(uri.AbsoluteUri),
                StatusCode = statusCode.HasValue ? (int)statusCode.Value : null,
                StatusDescription = SensitiveLogSanitizer.Sanitize(reasonPhrase ?? String.Empty),
                DurationMs = Math.Max(0, durationMs),
                ErrorType = exception?.GetType().Name,
                ErrorMessage = exception == null ? null : SensitiveLogSanitizer.Sanitize(exception.Message),
                Parameters = SanitizeParameters(parameters)
            };

            lock (SyncRoot) {
                Entries.AddFirst(entry);
                while (Entries.Count > MaxEntries) Entries.RemoveLast();
            }

            EntryAdded?.Invoke(null, entry);
        }

        public static IReadOnlyList<ApiDebugCallEntry> GetSnapshot() {
            lock (SyncRoot) {
                return Entries.ToList();
            }
        }

        public static void Clear() {
            lock (SyncRoot) {
                Entries.Clear();
            }
        }

        private static bool TryGetApiMethod(Uri uri, out string method) {
            method = null;
            if (uri == null) return false;
            if (!uri.Host.Contains("vk.", StringComparison.OrdinalIgnoreCase) && !uri.Host.Contains("vk.me", StringComparison.OrdinalIgnoreCase)) return false;

            string marker = "/method/";
            int index = uri.AbsolutePath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return false;

            method = uri.AbsolutePath.Substring(index + marker.Length).Trim('/');
            return !String.IsNullOrWhiteSpace(method);
        }

        private static Dictionary<string, string> SanitizeParameters(Dictionary<string, string> parameters) {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (parameters == null) return result;

            foreach (var parameter in parameters.OrderBy(p => p.Key)) {
                result[parameter.Key] = SensitiveLogSanitizer.IsSensitiveKey(parameter.Key)
                    ? "[REDACTED]"
                    : SensitiveLogSanitizer.Sanitize(parameter.Value ?? String.Empty);
            }

            return result;
        }
    }
}
