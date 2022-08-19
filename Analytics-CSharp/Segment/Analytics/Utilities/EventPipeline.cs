using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Segment.Concurrent;

namespace Segment.Analytics.Utilities
{
    internal class EventPipeline
    {
        private readonly Analytics _analytics;

        private readonly string _logTag;

        private readonly int _flushCount;

        private readonly long _flushIntervalInMillis;
        
        private readonly Channel<string> _writeChannel;

        private readonly Channel<string> _uploadChannel;

        private readonly AtomicInteger _eventCount;

        private readonly HTTPClient _httpClient;

        private readonly Storage _storage;
        
        internal string apiHost { get; set; }
        
        public bool running { get; private set; }

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
            this.apiHost = apiHost;

            _writeChannel = new Channel<string>();
            _uploadChannel = new Channel<string>();
            _eventCount = new AtomicInteger(0);
            _httpClient = new HTTPClient(analytics, apiKey);
            _storage = analytics.storage;
            running = false;
        }
        
        public void Put(string @event) {
            _writeChannel.Send(@event);
        }

        public void Flush()
        {
            _writeChannel.Send(FlushPoison);
        }

        public void Start()
        {
            running = true;
            Schedule();
            Write();
            Upload();
        }

        public void Stop()
        {
            _uploadChannel.Cancel();
            _writeChannel.Cancel();
            running = false;
        }

        private void Write() => _analytics.analyticsScope.Launch(_analytics.fileIODispatcher, async () =>
        {
            while (!_writeChannel.isCancelled)
            {
                var e = await _writeChannel.Receive();
                var isPoison = e.Equals(FlushPoison);

                if (!isPoison)
                {
                    try
                    {
                        await _storage.Write(Storage.Constants.Events, e);
                    }
                    catch (Exception exception)
                    {
                        Analytics.logger?.LogError(exception, "Error writing events to storage.");
                    }
                }

                if (_eventCount.IncrementAndGet() >= _flushCount || isPoison)
                {
                    _eventCount.Set(0);
                    _uploadChannel.Send(UploadSig);
                }
            }
        });

        private void Upload() => _analytics.analyticsScope.Launch(_analytics.networkIODispatcher, async () =>
        {
            while (!_uploadChannel.isCancelled)
            {
                await _uploadChannel.Receive();
                
                await Scope.WithContext(_analytics.fileIODispatcher, async () =>
                {
                    await _storage.Rollover();
                });

                var fileUrlList = _storage.Read(Storage.Constants.Events).Split(',').ToList();
                foreach (var url in fileUrlList)
                {
                    if (string.IsNullOrEmpty(url)) continue;
                    
                    var file = new FileInfo(url);
                    if (!file.Exists) continue;

                    var shouldCleanup = true;
                    try
                    {
                        shouldCleanup = await _httpClient.Upload(url);
                    }
                    catch (Exception e)
                    {
                        Analytics.logger?.LogError(e, "Error uploading to url");
                    }

                    if (shouldCleanup)
                    {
                        _storage.RemoveFile(url);
                    }
                }
            }
        });

        private void Schedule() => _analytics.analyticsScope.Launch(_analytics.fileIODispatcher, async () =>
        {
            if (_flushIntervalInMillis > 0)
            {
                while (running)
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