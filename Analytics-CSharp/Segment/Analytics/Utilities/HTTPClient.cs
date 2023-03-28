using global::System.Net.Http;
using global::System.Net.Http.Headers;
using global::System.Threading.Tasks;
using Segment.Serialization;

namespace Segment.Analytics.Utilities
{
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
                Analytics.s_logger?.LogError("Error " + response.StatusCode + " getting from settings url");
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
                Analytics.s_logger?.LogError("Error " + response.StatusCode + " uploading to url");

                switch (response.StatusCode)
                {
                    case var n when n >= 1 && n < 300:
                        return false;
                    case var n when n >= 300 && n < 400:
                        return false;
                    case 429:
                        return false;
                    case var n when n >= 400 && n < 500:
                        Analytics.s_logger?.LogError("Payloads were rejected by server. Marked for removal.");
                        return true;
                    default:
                        return false;
                }
            }

            return true;
        }

        public abstract Task<Response> DoGet(string url);

        public abstract Task<Response> DoPost(string url, byte[] data);

        public class Response
        {
            public int StatusCode { get; set; }
            public string Content { get; set; }
            public bool IsSuccessStatusCode => StatusCode >= 200 && StatusCode < 300;
        }
    }

    public class DefaultHTTPClient : HTTPClient
    {

        private readonly HttpClient _httpClient;

        public DefaultHTTPClient(string apiKey, string apiHost = null, string cdnHost = null) : base(apiKey, apiHost, cdnHost)
        {
            _httpClient = new HttpClient();
        }

        public override async Task<Response> DoGet(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = await _httpClient.SendAsync(request);
            return new Response
            {
                StatusCode = (int)response.StatusCode,
                Content = await response.Content.ReadAsStringAsync()
            };
        }

        public override async Task<Response> DoPost(string url, byte[] data)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
            request.Content = new ByteArrayContent(data);

            HttpResponseMessage response = await _httpClient.SendAsync(request);
            return new Response
            {
                StatusCode = (int)response.StatusCode
            };
        }
    }

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
