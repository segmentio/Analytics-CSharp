using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Segment.Serialization;

namespace Segment.Analytics.Utilities
{
    /// <summary>
    /// The template that defines the common logic that is required
    /// for a HTTPClient to fetch settings and to upload batches from/to Segment.
    /// Extend this class and implement the abstract methods if you want to handle
    /// http requests with a different library other than System.Net.
    /// </summary>
    public abstract class HTTPClient
    {
        internal const string DefaultAPIHost = "api.segment.io/v1";

        internal const string DefaultCdnHost = "cdn-settings.segment.com/v1";

        private readonly string _apiKey;

        protected readonly string _apiHost;

        protected readonly string _cdnHost;

        private readonly WeakReference<Analytics> _reference = new WeakReference<Analytics>(null);

        public Analytics AnalyticsRef
        {
            get => _reference.TryGetTarget(out Analytics analytics) ? analytics : null;
            set => _reference.SetTarget(value);
        }

        // --- Retry configuration (set from Configuration or httpConfig settings) ---

        public int MaxRetries { get; set; } = 10;

        public TimeSpan MaxTotalBackoffDuration { get; set; } = TimeSpan.FromHours(12);

        public TimeSpan MaxRateLimitDuration { get; set; } = TimeSpan.FromHours(12);

        public bool BackoffEnabled { get; set; } = true;

        public bool RateLimitEnabled { get; set; } = true;

        public int MaxRateLimitRetries { get; set; } = 10;

        public int MaxRetryAfterCapSeconds { get; set; } = 300;

        public double BaseBackoffMs { get; set; } = 500.0;

        public double MaxBackoffMs { get; set; } = 60_000.0;

        public Dictionary<int, string> StatusCodeOverrides { get; set; } = new Dictionary<int, string>();

        // -------------------------------------------------------------------------

        public HTTPClient(string apiKey, string apiHost = null, string cdnHost = null)
        {
            _apiKey = apiKey;
            _apiHost = apiHost ?? DefaultAPIHost;
            _cdnHost = cdnHost ?? DefaultCdnHost;
        }

        /// <summary>
        /// Returns formatted url to Segment's server.
        /// Override to use a custom server.
        /// </summary>
        public virtual string SegmentURL(string host, string path) => "https://" + host + path;

        public virtual async Task<Settings?> Settings()
        {
            string settingsURL = SegmentURL(_cdnHost, "/projects/" + _apiKey + "/settings");
            Settings? result = null;
            try
            {
                Response response = await DoGet(settingsURL);
                if (!response.IsSuccessStatusCode)
                {
                    AnalyticsRef?.ReportInternalError(AnalyticsErrorType.NetworkUnexpectedHttpCode,
                        message: "Error " + response.StatusCode + " getting from settings url");
                }
                else
                {
                    result = JsonUtility.FromJson<Settings>(response.Content);
                }
            }
            catch (Exception e)
            {
                AnalyticsRef?.ReportInternalError(AnalyticsErrorType.NetworkUnknown, e,
                    "Unknown network error when getting from settings url");
            }

            return result;
        }

        public virtual async Task<bool> Upload(byte[] data)
        {
            string uploadURL = SegmentURL(_apiHost, "/b");

            // Snapshot config at start of upload to avoid mid-loop mutation
            int maxRetries = MaxRetries;
            int maxRateLimitRetries = MaxRateLimitRetries;
            int retryAfterCapSeconds = MaxRetryAfterCapSeconds;
            TimeSpan maxTotalBackoff = MaxTotalBackoffDuration;
            TimeSpan maxRateLimit = MaxRateLimitDuration;
            bool backoffEnabled = BackoffEnabled;
            bool rateLimitEnabled = RateLimitEnabled;
            double backoffMs = BaseBackoffMs;
            double backoffCapMs = MaxBackoffMs;
            var overrides = StatusCodeOverrides;

            int totalAttempts = 0;
            int backoffAttempts = 0;
            int rateLimitAttempts = 0;
            DateTime? firstFailureTime = null;
            DateTime? rateLimitStartTime = null;

            while (true)
            {
                totalAttempts++;
                Response response = null;
                bool isNetworkError = false;

                try
                {
                    response = await DoPost(uploadURL, data, retryCount: totalAttempts - 1);
                }
                catch (Exception e)
                {
                    AnalyticsRef?.ReportInternalError(AnalyticsErrorType.NetworkUnknown, e,
                        "Unknown network error when uploading to url");
                    isNetworkError = true;
                }

                if (!isNetworkError)
                {
                    // 2xx and 3xx are success
                    if (IsSuccess(response.StatusCode))
                        return true;

                    Analytics.Logger.Log(LogLevel.Error,
                        message: "Error " + response.StatusCode + " uploading to url");

                    // 429 handling
                    if (response.StatusCode == 429)
                    {
                        if (!rateLimitEnabled)
                            return true; // rate limiting disabled — discard immediately

                        TimeSpan? retryAfter = ParseRetryAfter(response.RetryAfterHeader, retryAfterCapSeconds);
                        if (retryAfter.HasValue)
                        {
                            if (rateLimitStartTime == null) rateLimitStartTime = DateTime.UtcNow;
                            rateLimitAttempts++;
                            if (rateLimitAttempts > maxRateLimitRetries ||
                                DateTime.UtcNow - rateLimitStartTime.Value > maxRateLimit)
                            {
                                AnalyticsRef?.ReportInternalError(AnalyticsErrorType.NetworkServerLimited,
                                    message: "Max rate limit duration exceeded");
                                return true;
                            }
                            await Task.Delay(retryAfter.Value);
                            continue;
                        }
                        // No Retry-After — fall through to counted backoff
                    }

                    string action = GetStatusCodeAction(response.StatusCode, overrides);
                    if (action != "retry" || !backoffEnabled)
                    {
                        if (action != "retry")
                            AnalyticsRef?.ReportInternalError(AnalyticsErrorType.NetworkServerRejected,
                                message: "Response code: " + response.StatusCode + ". Non-retryable. Discarding batch.");
                        return true; // non-retryable or backoff disabled → discard
                    }
                }

                // Counted exponential backoff
                if (firstFailureTime == null) firstFailureTime = DateTime.UtcNow;
                if (DateTime.UtcNow - firstFailureTime.Value > maxTotalBackoff)
                {
                    Analytics.Logger.Log(LogLevel.Error, message: "Max total backoff duration exceeded");
                    return true; // discard — budget exhausted, no point retrying next cycle
                }

                backoffAttempts++;
                if (backoffAttempts > maxRetries)
                {
                    Analytics.Logger.Log(LogLevel.Error,
                        message: $"Retries exhausted after {totalAttempts} attempts");
                    return true; // discard — retry budget exhausted
                }

                await Task.Delay(TimeSpan.FromMilliseconds(backoffMs));
                backoffMs = Math.Min(backoffMs * 2, backoffCapMs);
            }
        }

        // --- Status classification helpers ---

        private static bool IsSuccess(int statusCode) => statusCode >= 200 && statusCode < 400;

        private static readonly int[] s_retryableClientErrors = { 408, 410, 429, 460 };
        private static readonly int[] s_nonRetryableServerErrors = { 501, 505, 511 };

        private static bool IsRetryable(int statusCode)
        {
            if (statusCode >= 500 && statusCode < 600)
                return Array.IndexOf(s_nonRetryableServerErrors, statusCode) < 0;
            return Array.IndexOf(s_retryableClientErrors, statusCode) >= 0;
        }

        private static string GetStatusCodeAction(int statusCode, Dictionary<int, string> overrides)
        {
            if (overrides != null && overrides.TryGetValue(statusCode, out string action))
                return action;
            return IsRetryable(statusCode) ? "retry" : "drop";
        }

        private static TimeSpan? ParseRetryAfter(string headerValue, int capSeconds = 300)
        {
            if (string.IsNullOrWhiteSpace(headerValue)) return null;
            if (!int.TryParse(headerValue.Trim(), out int seconds)) return null;
            if (seconds < 0) return null;
            return TimeSpan.FromSeconds(Math.Min(seconds, capSeconds));
        }

        // -----------------------------------------------------------------------

        /// <summary>Handle GET request</summary>
        public abstract Task<Response> DoGet(string url);

        /// <summary>Handle POST request</summary>
        public abstract Task<Response> DoPost(string url, byte[] data, int retryCount = 0);

        /// <summary>
        /// A wrapper class for http response, so that the HTTPClient is
        /// not dependent on a specific network library.
        /// </summary>
        public class Response
        {
            public int StatusCode { get; set; }

            public string Content { get; set; }

            /// <summary>Value of the Retry-After response header, or null if absent.</summary>
            public string RetryAfterHeader { get; set; }

            /// <summary>True for 2xx responses only (used by Settings()).</summary>
            public bool IsSuccessStatusCode => StatusCode >= 200 && StatusCode < 300;
        }
    }

    public class DefaultHTTPClient : HTTPClient
    {
        private readonly HttpClient _httpClient;

        public DefaultHTTPClient(string apiKey, string apiHost = null, string cdnHost = null)
            : base(apiKey, apiHost, cdnHost)
        {
            _httpClient = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            });
        }

        public override async Task<Response> DoGet(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Connection", "close");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await _httpClient.SendAsync(request);
            var result = new Response
            {
                StatusCode = (int)response.StatusCode,
                Content = await response.Content.ReadAsStringAsync()
            };
            response.Dispose();
            return result;
        }

        public override async Task<Response> DoPost(string url, byte[] data, int retryCount = 0)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (GZipStream gzip = new GZipStream(ms, CompressionMode.Compress, true))
                {
                    gzip.Write(data, 0, data.Length);
                }

                ms.Position = 0;
                StreamContent streamContent = new StreamContent(ms);
                streamContent.Headers.Add("Content-Encoding", "gzip");

                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Connection", "close");
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
                if (retryCount > 0)
                    request.Headers.Add("X-Retry-Count", retryCount.ToString());
                request.Content = streamContent;

                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string retryAfterHeader = null;
                if (response.Headers.TryGetValues("Retry-After", out var values))
                    retryAfterHeader = values.FirstOrDefault();

                var result = new Response
                {
                    StatusCode = (int)response.StatusCode,
                    RetryAfterHeader = retryAfterHeader
                };
                response.Dispose();
                return result;
            }
        }
    }

    /// <summary>
    /// A provider protocol that creates a HTTPClient with the given parameters
    /// </summary>
    public interface IHTTPClientProvider
    {
        HTTPClient CreateHTTPClient(string apiKey, string apiHost = null, string cdnHost = null);
    }

    public class DefaultHTTPClientProvider : IHTTPClientProvider
    {
        public HTTPClient CreateHTTPClient(string apiKey, string apiHost = null, string cdnHost = null)
        {
            return new DefaultHTTPClient(apiKey, apiHost, cdnHost);
        }
    }
}
