using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Segment.Serialization;

namespace Segment.Analytics.Utilities
{
    public class HTTPClient
    {
        internal const string DefaultAPIHost = "api.segment.io/v1";

        internal const string DefaultCdnHost = "cdn-settings.segment.com/v1";

        private readonly HttpClient _httpClient;

        private string _apiKey;
        
        private string _apiHost;
        
        private string _cdnHost;

        private string _authHeader;

        public HTTPClient(string apiKey, string apiHost = null, string cdnHost = null)
        {
            _apiKey = apiKey;
            _apiHost = apiHost ?? DefaultAPIHost;
            _cdnHost = cdnHost ?? DefaultCdnHost;
            _authHeader = AuthorizationHeader(apiKey);
            _httpClient = new HttpClient();
        }

        public string SegmentURL(string host, string path)
        {
            return "https://" + host + path;
        }
        
        public virtual async Task<Settings?> Settings()
        {
            var settingsURL = SegmentURL(_cdnHost, "/projects/" + _apiKey + "/settings");
            var response = await DoGet(settingsURL);
            Settings? result = null;
            
            if (!response.IsSuccessStatusCode)
            {
                Analytics.logger?.LogError("Error {Status} getting from settings url", response.StatusCode);
            }
            else
            {
                var json = await response.Content.ReadAsStringAsync();
                result = JsonUtility.FromJson<Settings>(json);
            }
                
            response.Dispose();
            return result;
        }

        public virtual async Task<bool> Upload(string file)
        {
            var uploadURL = SegmentURL(_apiHost, "/b");
            var response =  await DoUpload(uploadURL, file);

            if (!response.IsSuccessStatusCode)
            {
                Analytics.logger?.LogError("Error {Status} uploading to url", response.StatusCode);
                var responseCode = (int)response.StatusCode;
                response.Dispose();

                switch (responseCode)
                {
                    case var n when (n >= 1 && n < 300):
                        return false;
                    case var n when (n >= 300 && n < 400):
                        return false;
                    case 429:
                        return false;
                    case var n when (n >= 400 && n < 500):
                        Analytics.logger?.LogError("Payloads were rejected by server. Marked for removal.");
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
            
            return await _httpClient.SendAsync(request);
        }

        public async Task<HttpResponseMessage> DoPost(string url, byte[] data)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", _authHeader);
            request.Content = new ByteArrayContent(data);

            return await _httpClient.SendAsync(request);
        }

        private async Task<HttpResponseMessage> DoUpload(string url, string file)
        {
            var data = File.ReadAllBytes(file);
            return await DoPost(url, data);
        }

        private string AuthorizationHeader(string writeKey)
        {
            var bytesToEncode = Encoding.UTF8.GetBytes (writeKey + ":");
            return Convert.ToBase64String (bytesToEncode);
        }
    }
}