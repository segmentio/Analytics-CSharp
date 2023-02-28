namespace Segment.Analytics.Utilities
{
    using global::System;
    using global::System.Linq;
    using global::System.Threading.Tasks;
    using Segment.Concurrent;

    internal partial class EventPipeline
    {
        private readonly Analytics _analytics;

        private readonly string _logTag;

        private readonly int _flushCount;

        private readonly long _flushIntervalInMillis;

        private readonly Channel<string> _writeChannel;

        private readonly Channel<string> _uploadChannel;

        private readonly AtomicInteger _eventCount;

        private readonly HTTPClient _httpClient;

        private readonly IStorage _storage;

        internal string ApiHost { get; set; }

        public bool Running { get; private set; }

        internal const string FlushPoison = "#!flush";

        internal const string UploadSig = "#!upload";

        public EventPipeline(
            Analytics analytics,
            string logTag,
            string apiKey,
            int flushCount = 20,
            long flushIntervalInMillis = 30_000,
            string apiHost = HTTPClient.DefaultAPIHost)
        {
            this._analytics = analytics;
            this._logTag = logTag;
            this._flushCount = flushCount;
            this._flushIntervalInMillis = flushIntervalInMillis;
            this.ApiHost = apiHost;

            this._writeChannel = new Channel<string>();
            this._uploadChannel = new Channel<string>();
            this._eventCount = new AtomicInteger(0);
            this._httpClient = new HTTPClient(apiKey);
            this._storage = analytics.Storage;
            this.Running = false;
        }

        public void Put(string @event) => this._writeChannel.Send(@event);

        public void Flush() => this._writeChannel.Send(FlushPoison);

        public void Start()
        {
            this.Running = true;
            this.Schedule();
            this.Write();
            this.Upload();
        }

        public void Stop()
        {
            this._uploadChannel.Cancel();
            this._writeChannel.Cancel();
            this.Running = false;
        }

        private void Write() => this._analytics.AnalyticsScope.Launch(this._analytics.FileIODispatcher, async () =>
        {
            while (!this._writeChannel.isCancelled)
            {
                var e = await this._writeChannel.Receive();
                var isPoison = e.Equals(FlushPoison);

                if (!isPoison)
                {
                    try
                    {
                        await this._storage.Write(StorageConstants.Events, e);
                    }
                    catch (Exception exception)
                    {
                        Analytics.s_logger?.LogError(exception, this._logTag + ": Error writing events to storage.");
                    }
                }

                if (this._eventCount.IncrementAndGet() >= this._flushCount || isPoison)
                {
                    this._eventCount.Set(0);
                    this._uploadChannel.Send(UploadSig);
                }
            }
        });

        private void Upload() => this._analytics.AnalyticsScope.Launch(this._analytics.NetworkIODispatcher, async () =>
        {
            while (!this._uploadChannel.isCancelled)
            {
                _ = await this._uploadChannel.Receive();

                await Scope.WithContext(this._analytics.FileIODispatcher, async () => await this._storage.Rollover());

                var fileUrlList = this._storage.Read(StorageConstants.Events).Split(',').ToList();
                foreach (var url in fileUrlList)
                {
                    if (string.IsNullOrEmpty(url))
                    {
                        continue;
                    }

                    var data = this._storage.ReadAsBytes(url);
                    if (data == null)
                    {
                        continue;
                    }

                    var shouldCleanup = true;
                    try
                    {
                        shouldCleanup = await this._httpClient.Upload(data);
                    }
                    catch (Exception e)
                    {
                        Analytics.s_logger?.LogError(e, this._logTag + ": Error uploading to url");
                    }

                    if (shouldCleanup)
                    {
                        _ = this._storage.RemoveFile(url);
                    }
                }
            }
        });

        private void Schedule() => this._analytics.AnalyticsScope.Launch(this._analytics.FileIODispatcher, async () =>
        {
            if (this._flushIntervalInMillis > 0)
            {
                while (this.Running)
                {
                    this.Flush();

                    // use delay to do periodical task
                    // this is doable in coroutine, since delay only suspends, allowing thread to
                    // do other work and then come back. 
                    await Task.Delay((int)this._flushIntervalInMillis);
                }
            }
        });
    }
}
