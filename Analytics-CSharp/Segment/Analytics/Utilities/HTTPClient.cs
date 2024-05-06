using System;
using System.IO;
using System.IO.Compression;
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
            get
            {
                return _reference.TryGetTarget(out Analytics analytics) ? analytics : null;
            }
            set
            {
                _reference.SetTarget(value);
            }
        }

        public HTTPClient(string apiKey, string apiHost = null, string cdnHost = null)
        {
            _apiKey = apiKey;
            _apiHost = apiHost ?? DefaultAPIHost;
            _cdnHost = cdnHost ?? DefaultCdnHost;
        }

        /// <summary>
        /// Returns formatted url to Segment's server.
        /// If you want to use your own server, override this method like the following
        /// <code>
        ///     public virtual string SegmentURL(string host, string path)
        ///     {
        ///         if (host is cdnHost)
        ///         {
        ///             return cdn url with your own path
        ///         }
        ///         else { // is apiHost
        ///             return api url with your own path
        ///         }
        ///     }
        /// </code>
        /// </summary>
        /// <param name="host">cdnHost or apiHost</param>
        /// <param name="path">Path to segment's /settings endpoint or /b endpoints</param>
        /// <returns>Formatted url</returns>
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
                    AnalyticsRef?.ReportInternalError(AnalyticsErrorType.NetworkUnexpectedHttpCode, message: "Error " + response.StatusCode + " getting from settings url");
                }
                else
                {
                    string json = response.Content;
                    result = JsonUtility.FromJson<Settings>(json);
                }
            }
            catch (Exception e)
            {
                AnalyticsRef?.ReportInternalError(AnalyticsErrorType.NetworkUnknown, e, "Unknown network error when getting from settings url");
            }

            return result;
        }

        public virtual async Task<bool> Upload(byte[] data)
        {
            string uploadURL = SegmentURL(_apiHost, "/b");
            try
            {
                Response response = await DoPost(uploadURL, data);

                if (!response.IsSuccessStatusCode)
                {
                    Analytics.Logger.Log(LogLevel.Error, message: "Error " + response.StatusCode + " uploading to url");

                    switch (response.StatusCode)
                    {
                        case var n when n >= 1 && n < 300:
                            return false;
                        case var n when n >= 300 && n < 400:
                            AnalyticsRef?.ReportInternalError(AnalyticsErrorType.NetworkUnexpectedHttpCode, message: "Response code: " + n);
                            return false;
                        case 429:
                            AnalyticsRef?.ReportInternalError(AnalyticsErrorType.NetworkServerLimited, message: "Response code: 429");
                            return false;
                        case var n when n >= 400 && n < 500:
                            AnalyticsRef?.ReportInternalError(AnalyticsErrorType.NetworkServerRejected, message: "Response code: " + n + ". Payloads were rejected by server. Marked for removal.");
                            return true;
                        default:
                            return false;
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                AnalyticsRef?.ReportInternalError(AnalyticsErrorType.NetworkUnknown, e, "Unknown network error when uploading to url");
            }

            return false;
        }

        /// <summary>
        /// Handle GET request
        /// </summary>
        /// <param name="url">URL where the GET request sent to</param>
        /// <returns>Awaitable response of the GET request</returns>
        public abstract Task<Response> DoGet(string url);

        /// <summary>
        /// Handle POST request
        /// </summary>
        /// <param name="url">URL where the POST request sent to</param>
        /// <param name="data">data to upload</param>
        /// <returns>Awaitable response of the POST request</returns>
        public abstract Task<Response> DoPost(string url, byte[] data);

        /// <summary>
        /// A wrapper class for http response, so that the HTTPClient is
        /// not dependent on a specific network library.
        /// </summary>
        public class Response
        {
            /// <summary>
            /// Status code of the http request
            /// </summary>
            public int StatusCode { get; set; }

            /// <summary>
            /// Response content of the http request
            /// </summary>
            public string Content { get; set; }

            /// <summary>
            /// A convenient method to check if the http request is successful
            /// </summary>
            public bool IsSuccessStatusCode => StatusCode >= 200 && StatusCode < 300;
        }
    }

    public class DefaultHTTPClient : HTTPClient
    {

        private readonly HttpClient _httpClient;

        public DefaultHTTPClient(string apiKey, string apiHost = null, string cdnHost = null) : base(apiKey, apiHost, cdnHost)
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

        public override async Task<Response> DoPost(string url, byte[] data)
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
                request.Content = streamContent;

                HttpResponseMessage response = await _httpClient.SendAsync(request);
                var result = new Response {StatusCode = (int)response.StatusCode};
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
