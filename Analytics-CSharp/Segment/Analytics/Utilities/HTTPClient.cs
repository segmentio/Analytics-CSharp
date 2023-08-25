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

        private readonly string _apiHost;

        private readonly string _cdnHost;

        public HTTPClient(string apiKey, string apiHost = null, string cdnHost = null)
        {
            _apiKey = apiKey;
            _apiHost = apiHost ?? DefaultAPIHost;
            _cdnHost = cdnHost ?? DefaultCdnHost;
        }

        public string SegmentURL(string host, string path) => "https://" + host + path;

        public virtual async Task<Settings?> Settings()
        {
            string settingsURL = SegmentURL(_cdnHost, "/projects/" + _apiKey + "/settings");
            Response response = await DoGet(settingsURL);
            Settings? result = null;

            if (!response.IsSuccessStatusCode)
            {
                Analytics.Logger?.Log(LogLevel.Error, message: "Error " + response.StatusCode + " getting from settings url");
            }
            else
            {
                string json = response.Content;
                result = JsonUtility.FromJson<Settings>(json);
            }
            return result;
        }

        public virtual async Task<bool> Upload(byte[] data)
        {
            string uploadURL = SegmentURL(_apiHost, "/b");
            Response response = await DoPost(uploadURL, data);

            if (!response.IsSuccessStatusCode)
            {
                Analytics.Logger?.Log(LogLevel.Error, message: "Error " + response.StatusCode + " uploading to url");

                switch (response.StatusCode)
                {
                    case var n when n >= 1 && n < 300:
                        return false;
                    case var n when n >= 300 && n < 400:
                        return false;
                    case 429:
                        return false;
                    case var n when n >= 400 && n < 500:
                        Analytics.Logger?.Log(LogLevel.Error, message: "Payloads were rejected by server. Marked for removal.");
                        return true;
                    default:
                        return false;
                }
            }

            return true;
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
