using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using global::System;
using global::System.Linq;
using Segment.Analytics.Policies;
using Segment.Concurrent;
using Segment.Serialization;

namespace Segment.Analytics.Utilities
{
    internal sealed class FlushEvent : RawEvent
    {
        public override string Type => "flush";
        public readonly SemaphoreSlim _semaphore;

        internal FlushEvent(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }
   }


    public class SyncEventPipeline: IEventPipeline
    {
        private readonly Analytics _analytics;

        private readonly string _logTag;

        private readonly IList<IFlushPolicy> _flushPolicies;

        private Channel<RawEvent> _writeChannel;

        private Channel<FlushEvent> _uploadChannel;

        private readonly HTTPClient _httpClient;

        private readonly IStorage _storage;

        public string ApiHost { get; set; }

        public bool Running { get; private set; }

        internal int _flushTimeout = -1;
        internal CancellationToken _flushCancellationToken = CancellationToken.None;

        public SyncEventPipeline(
            Analytics analytics,
            string logTag,
            string apiKey,
            IList<IFlushPolicy> flushPolicies,
            string apiHost = HTTPClient.DefaultAPIHost,
            int flushTimeout = -1,
            CancellationToken? flushCancellationToken = null)
        {
            _analytics = analytics;
            _logTag = logTag;
            _flushPolicies = flushPolicies;
            ApiHost = apiHost;

            _writeChannel = new Channel<RawEvent>();
            _uploadChannel = new Channel<FlushEvent>();
            _httpClient = analytics.Configuration.HttpClientProvider.CreateHTTPClient(apiKey, apiHost: apiHost);
            _httpClient.AnalyticsRef = analytics;
            _storage = analytics.Storage;
            Running = false;
            _flushTimeout = flushTimeout;
            _flushCancellationToken = flushCancellationToken ?? CancellationToken.None;
        }

        public void Put(RawEvent @event) => _writeChannel.Send(@event);

        public void Flush() {
            if (Running && !_uploadChannel.isCancelled)
            {
                FlushEvent flushEvent = new FlushEvent(new SemaphoreSlim(0));
                _writeChannel.Send(flushEvent);
                flushEvent._semaphore.Wait(_flushTimeout, _flushCancellationToken);
            }
        } 

        public void Start()
        {
            if (Running) return;

            // avoid to re-establish a channel if the pipeline just gets created
            if (_writeChannel.isCancelled)
            {
                _writeChannel = new Channel<RawEvent>();
                _uploadChannel = new Channel<FlushEvent>();
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
            Unschedule();
        }

        private void Write() => _analytics.AnalyticsScope.Launch(_analytics.FileIODispatcher, async () =>
        {
            while (!_writeChannel.isCancelled)
            {
                RawEvent e = await _writeChannel.Receive();
                bool isPoison = e is FlushEvent;

                if (!isPoison)
                {
                    try
                    {
                        string str = JsonUtility.ToJson(e);
                        Analytics.Logger.Log(LogLevel.Debug, message: _logTag + " running " + str);
                        await _storage.Write(StorageConstants.Events, str);

                        foreach (IFlushPolicy flushPolicy in _flushPolicies)
                        {
                            flushPolicy.UpdateState(e);
                        }
                    }
                    catch (Exception exception)
                    {
                        Analytics.Logger.Log(LogLevel.Error, exception, _logTag + ": Error writing events to storage.");
                    }
                }

                if (isPoison || _flushPolicies.Any(o => o.ShouldFlush()))
                {
                    FlushEvent flushEvent = e as FlushEvent ?? new FlushEvent(null);
                    _uploadChannel.Send(flushEvent);
                    foreach (IFlushPolicy flushPolicy in _flushPolicies)
                    {
                        flushPolicy.Reset();
                    }
                }
            }
        });

        private void Upload() => _analytics.AnalyticsScope.Launch(_analytics.NetworkIODispatcher, async () =>
        {
            while (!_uploadChannel.isCancelled)
            {
                FlushEvent flushEvent = await _uploadChannel.Receive();
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
                flushEvent._semaphore?.Release();
            }
        });

        private void Schedule()
        {
            foreach (IFlushPolicy flushPolicy in _flushPolicies)
            {
                flushPolicy.Schedule(_analytics);
            }
        }

        private void Unschedule()
        {
            foreach (IFlushPolicy flushPolicy in _flushPolicies)
            {
                flushPolicy.Unschedule();
            }
        }
    }
}
