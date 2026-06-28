using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ELOR.Laney.Core.Logging;

namespace ELOR.Laney.Core.Network {
    public readonly struct LNetQueueSnapshot {
        public int ActiveRequests { get; }
        public int SequentialGetRequests { get; }
        public int SequentialPostRequests { get; }

        public LNetQueueSnapshot(int activeRequests, int sequentialGetRequests, int sequentialPostRequests) {
            ActiveRequests = activeRequests;
            SequentialGetRequests = sequentialGetRequests;
            SequentialPostRequests = sequentialPostRequests;
        }
    }

    public class LNet {
        static HttpClient _defaultClient;
        static HttpClient _zstdClient;
        static Queue<Task<HttpResponseMessage>> _getRequests = new Queue<Task<HttpResponseMessage>>();
        static Queue<Task<HttpResponseMessage>> _postRequests = new Queue<Task<HttpResponseMessage>>();
        static int _activeRequests;
        static int _sequentialGetRequests;
        static int _sequentialPostRequests;

        public static event EventHandler<string> DebugLog;
        private static void Log(string text) {
            Debug.WriteLine($"LNet: {text}");
            DebugLog?.Invoke(null, text);
        }

        public static LNetQueueSnapshot GetQueueSnapshot() {
            return new LNetQueueSnapshot(
                Volatile.Read(ref _activeRequests),
                Volatile.Read(ref _sequentialGetRequests),
                Volatile.Read(ref _sequentialPostRequests));
        }

        private static HttpClient GetConfiguredHttpClient() {
            return new HttpClient(new HttpClientHandler() {
                AllowAutoRedirect = true,
                MaxConnectionsPerServer = 50,
                AutomaticDecompression = DecompressionMethods.All
            }, false) {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        public static async Task<HttpResponseMessage> GetAsync(Uri uri,
            Dictionary<string, string> parameters = null,
            Dictionary<string, string> headers = null,
            CancellationTokenSource cts = null) {
            return await InternalSendRequestAsync(uri, parameters, cts, headers, HttpMethod.Get, true);
        }

        public static async Task<HttpResponseMessage> PostAsync(Uri uri,
            Dictionary<string, string> parameters = null,
            Dictionary<string, string> headers = null,
            CancellationTokenSource cts = null) {
            return await InternalSendRequestAsync(uri, parameters, cts, headers, HttpMethod.Post, true);
        }

        public static async Task<HttpResponseMessage> GetSequentialAsync(Uri uri,
            Dictionary<string, string> parameters = null,
            Dictionary<string, string> headers = null,
            CancellationTokenSource cts = null) {

            if (_getRequests.Count > 0) {
                var last = _getRequests.Peek();
                await last.WaitAsync(new CancellationTokenSource().Token);
            }

            var task = InternalSendRequestAsync(uri, parameters, cts, headers, HttpMethod.Get);
            _getRequests.Enqueue(task);
            Interlocked.Increment(ref _sequentialGetRequests);

            try {
                return await task;
            } finally {
                if (_getRequests.Count > 0) _ = _getRequests.Dequeue();
                Interlocked.Decrement(ref _sequentialGetRequests);
            }
        }

        public static async Task<HttpResponseMessage> PostSequentialAsync(Uri uri,
            Dictionary<string, string> parameters = null,
            Dictionary<string, string> headers = null,
            CancellationTokenSource cts = null) {

            if (_postRequests.Count > 0) {
                var last = _postRequests.Peek();
                await last.WaitAsync(new CancellationTokenSource().Token);
            }

            var task = InternalSendRequestAsync(uri, parameters, cts, headers, HttpMethod.Post);
            _postRequests.Enqueue(task);
            Interlocked.Increment(ref _sequentialPostRequests);

            try {
                return await task;
            } finally {
                if (_postRequests.Count > 0) _ = _postRequests.Dequeue();
                Interlocked.Decrement(ref _sequentialPostRequests);
            }
        }

        private static async Task<HttpResponseMessage> InternalSendRequestAsync(Uri uri, Dictionary<string, string> parameters, CancellationTokenSource cts, Dictionary<string, string> headers = null, HttpMethod httpMethod = null, bool returnAfterHeaderReads = false) {
            if (httpMethod == null) httpMethod = HttpMethod.Get;

            bool isZstdRequest = headers?.ContainsKey("Accept-Encoding") == true && headers["Accept-Encoding"].Contains("zstd");

            HttpRequestMessage hrm = new HttpRequestMessage(httpMethod, uri) {
                Version = new Version(2, 0)
            };
            if (headers != null) foreach (var header in headers) {
                    hrm.Headers.Add(header.Key, header.Value);
                }
            if (parameters != null) hrm.Content = new FormUrlEncodedContent(parameters);

            HttpClient client = null;
            if (isZstdRequest) {
                if (_zstdClient == null) _zstdClient = GetConfiguredHttpClient();
                _zstdClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("zstd"));
                client = _zstdClient;
            } else {
                if (_defaultClient == null) _defaultClient = GetConfiguredHttpClient();
                _defaultClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
                _defaultClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
                client = _defaultClient;
            }

            client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
            if (!client.DefaultRequestHeaders.Contains("User-Agent")) client.DefaultRequestHeaders.Add("User-Agent", App.UserAgent);

#if RELEASE
            string paramstr = "";
#else
            StringBuilder paramstr = new StringBuilder();
            if (parameters != null) {
                paramstr.Append(" | ");
                foreach (var p in parameters) {
                    if (SensitiveLogSanitizer.IsSensitiveKey(p.Key) || p.Key == "code") continue;
                    string value = p.Value.Replace("\n", "").Replace("\r\n", "");
                    paramstr.Append($"{p.Key}={SensitiveLogSanitizer.Sanitize(value)}; ");
                }
            }
#endif
#if !RELEASE
            if (Settings.LNetLogs) Log($"=> {SensitiveLogSanitizer.Sanitize(uri.AbsoluteUri)} | {httpMethod.Method}{paramstr}");
#endif
            var completionOption = returnAfterHeaderReads ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead;
            var stopwatch = Stopwatch.StartNew();
            HttpResponseMessage result = null;
            try {
                Interlocked.Increment(ref _activeRequests);
                result = cts == null ? await client.SendAsync(hrm, completionOption)
                    : await client.SendAsync(hrm, completionOption, cts.Token);
            } catch (Exception ex) {
                stopwatch.Stop();
                ApiDebugMonitor.Record(uri, parameters, httpMethod.Method, null, null, stopwatch.ElapsedMilliseconds, ex);
                throw;
            } finally {
                Interlocked.Decrement(ref _activeRequests);
            }
            stopwatch.Stop();

#if !RELEASE
            if (Settings.LNetLogs) Log($"<= {SensitiveLogSanitizer.Sanitize(uri.AbsoluteUri)} | Code: {result.StatusCode} | {stopwatch.ElapsedMilliseconds} ms.");
#endif
            ApiDebugMonitor.Record(uri, parameters, httpMethod.Method, result.StatusCode, result.ReasonPhrase, stopwatch.ElapsedMilliseconds);
            return result;
        }
    }
}
