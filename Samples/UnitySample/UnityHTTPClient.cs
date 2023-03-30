using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Segment.Analytics.Utilities;
using UnityEngine.Networking;

namespace UnitySample
{
    /// <summary>
    /// Http client based on UnityWebRequest.
    /// NOTE: this implementation shows how you can customize the network logic with your favorite network library.
    /// Though it is based on UnityWebRequest, it does not work in WebGL due to the fact that the Analytics library
    /// is architected to be a multi-thread library, whereas WebGL does not support multi-threading as of right now.
    /// </summary>
    public class UnityHTTPClient : HTTPClient
    {
        public UnityHTTPClient(string apiKey, string apiHost = null, string cdnHost = null) : base(apiKey, apiHost, cdnHost)
        {
        }

        public override async Task<Response> DoGet(string url)
        {
            using (var request = new NetworkRequest {URL = url, Action = GetRequest})
            {

                MainThreadDispatcher.Instance.Post(request);
                await request.Semaphore.WaitAsync();

                return request.Response;
            }
        }

        IEnumerator GetRequest(NetworkRequest networkRequest)
        {
            using (var request = UnityWebRequest.Get(networkRequest.URL))
            {
                request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
                yield return request.SendWebRequest();

                networkRequest.Response.StatusCode = (int)request.responseCode;
                networkRequest.Response.Content = request.downloadHandler.text;
                networkRequest.Semaphore.Release();
            }
        }

        public override async Task<Response> DoPost(string url, byte[] data)
        {
            using (var request = new NetworkRequest {URL = url, Data = data, Action = PostRequest})
            {
                MainThreadDispatcher.Instance.Post(request);
                await request.Semaphore.WaitAsync();

                return request.Response;
            }
        }

        IEnumerator PostRequest(NetworkRequest networkRequest)
        {
            using (var request = UnityWebRequest.Put(networkRequest.URL, networkRequest.Data))
            {
                request.SetRequestHeader("Content-Type", "text/plain");
                yield return request.SendWebRequest();

                networkRequest.Response.StatusCode = (int)request.responseCode;
                networkRequest.Semaphore.Release();
            }
        }
    }

    public class UnityHTTPClientProvider : IHTTPClientProvider
    {
        /// <summary>
        /// Provider that creates a Http client based on UnityWebRequest
        /// </summary>
        /// <param name="dispatcher">the dispatcher is required to force instantiation of MainThreadDispatcher</param>
        public UnityHTTPClientProvider(MainThreadDispatcher dispatcher)
        {
        }

        public HTTPClient CreateHTTPClient(string apiKey, string apiHost = null, string cdnHost = null)
        {
            return new UnityHTTPClient(apiKey, apiHost, cdnHost);
        }
    }

    public class NetworkRequest : IDisposable
    {
        public HTTPClient.Response Response { get; set; }
        public SemaphoreSlim Semaphore { get; set; }

        public string URL { get; set; }

        public byte[] Data { get; set; }

        public Func<NetworkRequest, IEnumerator> Action { get; set; }

        public NetworkRequest()
        {
            Response = new HTTPClient.Response();
            Semaphore = new SemaphoreSlim(0);
        }

        public IEnumerator Run() => Action(this);

        public void Dispose()
        {
            Semaphore?.Dispose();
        }
    }

    public class MainThreadDispatcher : Singleton<MainThreadDispatcher>
    {
        private ConcurrentQueue<NetworkRequest> _tasks;

        protected override void Awake()
        {
            base.Awake();
            _tasks= new ConcurrentQueue<NetworkRequest>();
        }

        public void Post(NetworkRequest task)
        {
            _tasks.Enqueue(task);
        }


        private void Update()
        {
            while (!_tasks.IsEmpty)
            {
                if (_tasks.TryDequeue(out NetworkRequest task))
                {
                    StartCoroutine(task.Run());
                }
            }
        }
    }
}
