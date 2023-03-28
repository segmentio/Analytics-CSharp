using System;
using System.IO;
using System.Text;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Segment.Analytics.Utilities;
using UnityEngine.Networking;
using UnityEngine;

namespace UnitySample
{
    public class UnityHTTPClient : HTTPClient
    {
        public UnityHTTPClient(string apiKey, string apiHost = null, string cdnHost = null) : base(apiKey, apiHost, cdnHost)
        {
        }

        public override async Task<Response> DoGet(string url)
        {
            var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");

            await request.SendWebRequest();
            var result = new Response
            {
                StatusCode = (int) request.responseCode,
                Content = request.downloadHandler.text
            };
            request.Dispose();

            return result;
        }

        public override async Task<Response> DoPost(string url, byte[] data)
        {
            var request = UnityWebRequest.Put(url, data);
            request.SetRequestHeader("Content-Type", "text/plain");

            await request.SendWebRequest();
            var result = new Response
            {
                StatusCode = (int) request.responseCode
            };
            request.Dispose();

            return result;
        }
    }

    public class UnityHTTPClientProvider : IHTTPClientProvider
    {
        public HTTPClient CreateHTTPClient(string apiKey, string apiHost = null, string cdnHost = null)
        {
            return new UnityHTTPClient(apiKey, apiHost, cdnHost);
        }
    }

    #region Async UnityWebRequest

    /**
     * Add `GetAwaiter` to `AsyncOperation` to make
     * operations of UnityWebRequest awaitable thus avoiding to use coroutine
     * see: https://gist.github.com/mattyellen/d63f1f557d08f7254345bff77bfdc8b3
     */
    public static partial class ExtensionMethods
    {
        public static TaskAwaiter GetAwaiter(this AsyncOperation asyncOp)
        {
            var tcs = new TaskCompletionSource<object>();
            asyncOp.completed += obj => { tcs.SetResult(null); };
            return ((Task)tcs.Task).GetAwaiter();
        }
    }

    #endregion
}
