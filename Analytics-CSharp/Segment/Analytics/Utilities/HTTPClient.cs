namespace Segment.Analytics.Utilities
{
    using global::System;
    using global::System.Net.Http;
    using global::System.Net.Http.Headers;
    using global::System.Text;
    using global::System.Threading.Tasks;
    using Segment.Serialization;

    public class HTTPClient
    {
        internal const string DefaultAPIHost = "api.segment.io/v1";

        internal const string DefaultCdnHost = "cdn-settings.segment.com/v1";

        private readonly HttpClient _httpClient;

        private readonly string _apiKey;

        private readonly string _apiHost;

        private readonly string _cdnHost;

        private readonly string _authHeader;

        public HTTPClient(string apiKey, string apiHost = null, string cdnHost = null)
        {
            this._apiKey = apiKey;
            this._apiHost = apiHost ?? DefaultAPIHost;
            this._cdnHost = cdnHost ?? DefaultCdnHost;
            this._authHeader = this.AuthorizationHeader(apiKey);
            this._httpClient = new HttpClient();
        }

        public string SegmentURL(string host, string path) => "https://" + host + path;

        public virtual async Task<Settings?> Settings()
        {
            var settingsURL = this.SegmentURL(this._cdnHost, "/projects/" + this._apiKey + "/settings");
            var response = await this.DoGet(settingsURL);
            Settings? result = null;

            if (!response.IsSuccessStatusCode)
            {
                Analytics.s_logger?.LogError("Error " + response.StatusCode + " getting from settings url");
            }
            else
            {
                var json = await response.Content.ReadAsStringAsync();
                result = JsonUtility.FromJson<Settings>(json);
            }

            response.Dispose();
            return result;
        }

        public virtual async Task<bool> Upload(byte[] data)
        {
            var uploadURL = this.SegmentURL(this._apiHost, "/b");
            var response = await this.DoPost(uploadURL, data);

            if (!response.IsSuccessStatusCode)
            {
                Analytics.s_logger?.LogError("Error " + response.StatusCode + " uploading to url");
                var responseCode = (int)response.StatusCode;
                response.Dispose();

                switch (responseCode)
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

            response.Dispose();
            return true;
        }

        public async Task<HttpResponseMessage> DoGet(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return await this._httpClient.SendAsync(request);
        }

        public async Task<HttpResponseMessage> DoPost(string url, byte[] data)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", this._authHeader);
            request.Content = new ByteArrayContent(data);

            return await this._httpClient.SendAsync(request);
        }

        private string AuthorizationHeader(string writeKey)
        {
            var bytesToEncode = Encoding.UTF8.GetBytes(writeKey + ":");
            return Convert.ToBase64String(bytesToEncode);
        }
    }
}
