using global::System;
using global::System.Linq;
using global::System.Threading.Tasks;
using Segment.Concurrent;

namespace Segment.Analytics.Utilities
{
    internal class EventPipeline
    {
        private readonly Analytics _analytics;

        private readonly string _logTag;

        private readonly int _flushCount;

        private readonly long _flushIntervalInMillis;

        private Channel<string> _writeChannel;

        private Channel<string> _uploadChannel;

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
            _analytics = analytics;
            _logTag = logTag;
            _flushCount = flushCount;
            _flushIntervalInMillis = flushIntervalInMillis;
            ApiHost = apiHost;

            _writeChannel = new Channel<string>();
            _uploadChannel = new Channel<string>();
            _eventCount = new AtomicInteger(0);
            _httpClient = analytics.Configuration.HttpClientProvider.CreateHTTPClient(apiKey);
            _httpClient.AnalyticsRef = analytics;
            _storage = analytics.Storage;
            Running = false;
        }

        public void Put(string @event) => _writeChannel.Send(@event);

        public void Flush() => _writeChannel.Send(FlushPoison);

        public void Start()
        {
            if (Running) return;

            // avoid to re-establish a channel if the pipeline just gets created
            if (_writeChannel.isCancelled)
            {
                _writeChannel = new Channel<string>();
                _uploadChannel = new Channel<string>();
            }

            Running = true;
            Schedule();
            Write();
            Upload();
        }

        public void Stop()
        {
            if (!Running) return;
            Running = false;

            _uploadChannel.Cancel();
            _writeChannel.Cancel();
        }

        private void Write() => _analytics.AnalyticsScope.Launch(_analytics.FileIODispatcher, async () =>
        {
            while (!_writeChannel.isCancelled)
            {
                string e = await _writeChannel.Receive();
                bool isPoison = e.Equals(FlushPoison);

                if (!isPoison)
                {
                    try
                    {
                        Analytics.Logger.Log(LogLevel.Debug, message: _logTag + " running " + e);
                        await _storage.Write(StorageConstants.Events, e);
                    }
                    catch (Exception exception)
                    {
                        Analytics.Logger.Log(LogLevel.Error, exception, _logTag + ": Error writing events to storage.");
                    }
                }

                if (_eventCount.IncrementAndGet() >= _flushCount || isPoison)
                {
                    _eventCount.Set(0);
                    _uploadChannel.Send(UploadSig);
                }
            }
        });

        private void Upload() => _analytics.AnalyticsScope.Launch(_analytics.NetworkIODispatcher, async () =>
        {
            while (!_uploadChannel.isCancelled)
            {
                await _uploadChannel.Receive();
                Analytics.Logger.Log(LogLevel.Debug, message: _logTag + " performing flush");

                await Scope.WithContext(_analytics.FileIODispatcher, async () => await _storage.Rollover());

                string[] fileUrlList = _storage.Read(StorageConstants.Events).Split(',');
                foreach (string url in fileUrlList)
                {
                    if (string.IsNullOrEmpty(url))
                    {
                        continue;
                    }

                    byte[] data = _storage.ReadAsBytes(url);
                    if (data == null)
                    {
                        continue;
                    }

                    bool shouldCleanup = true;
                    try
                    {
                        shouldCleanup = await _httpClient.Upload(data);
                        Analytics.Logger.Log(LogLevel.Debug, message: _logTag + " uploaded " + url);
                    }
                    catch (Exception e)
                    {
                        Analytics.Logger.Log(LogLevel.Error, e, _logTag + ": Error uploading to url");
                    }

                    if (shouldCleanup)
                    {
                        _storage.RemoveFile(url);
                    }
                }
            }
        });

        private void Schedule() => _analytics.AnalyticsScope.Launch(_analytics.FileIODispatcher, async () =>
        {
            if (_flushIntervalInMillis > 0)
            {
                while (Running)
                {
                    Flush();

                    // use delay to do periodical task
                    // this is doable in coroutine, since delay only suspends, allowing thread to
                    // do other work and then come back.
                    await Task.Delay((int)_flushIntervalInMillis);
                }
            }
        });
    }
}
