using System.Collections.Generic;
using System.Threading;
using global::System;
using global::System.Linq;
using Segment.Analytics.Policies;
using Segment.Concurrent;
using Segment.Serialization;

namespace Segment.Analytics.Utilities
{
    public class SyncEventPipeline: IEventPipeline
    {
        private readonly Analytics _analytics;

        private readonly string _logTag;

        private readonly IList<IFlushPolicy> _flushPolicies;

        private Channel<RawEvent> _writeChannel;

        private Channel<string> _uploadChannel;
        private Channel<string> _flushCompleteChannel;

        private readonly HTTPClient _httpClient;

        private readonly IStorage _storage;

        internal string ApiHost { get; set; }

        public bool Running { get; private set; }

        internal const string FlushPoison = "#!flush";

        internal static readonly RawEvent s_flushEvent = new ScreenEvent(FlushPoison, FlushPoison);

        internal const string UploadSig = "#!upload";
        internal const string FlushCompleteSig = "#!complete";
        internal int _flushTimeout = 5000;
        internal CancellationToken? _flushCancellationToken = null;

        public SyncEventPipeline(
            Analytics analytics,
            string logTag,
            string apiKey,
            IList<IFlushPolicy> flushPolicies,
            string apiHost = HTTPClient.DefaultAPIHost,
            int flushTimeout = 5000,
            CancellationToken? flushCancellationToken = null)
        {
            _analytics = analytics;
            _logTag = logTag;
            _flushPolicies = flushPolicies;
            ApiHost = apiHost;

            _writeChannel = new Channel<RawEvent>();
            _uploadChannel = new Channel<string>();
            _flushCompleteChannel = null;
            _httpClient = analytics.Configuration.HttpClientProvider.CreateHTTPClient(apiKey, apiHost: apiHost);
            _httpClient.AnalyticsRef = analytics;
            _storage = analytics.Storage;
            Running = false;
            _flushTimeout = flushTimeout;
            _flushCancellationToken = flushCancellationToken;
        }

        public void Put(RawEvent @event) => _writeChannel.Send(@event);

        public void Flush() {
            _flushCompleteChannel = new Channel<string>();
            _writeChannel.Send(s_flushEvent);
            _flushCompleteChannel.Receive().Wait(_flushTimeout,_flushCancellationToken??CancellationToken.None);
            _flushCompleteChannel = null;
        } 

        public void Start()
        {
            if (Running) return;

            // avoid to re-establish a channel if the pipeline just gets created
            if (_writeChannel.isCancelled)
            {
                _writeChannel = new Channel<RawEvent>();
                _uploadChannel = new Channel<string>();
                _flushCompleteChannel = new Channel<string>();
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
            _flushCompleteChannel.Cancel();
            _writeChannel.Cancel();
            Unschedule();
        }

        private void Write() => _analytics.AnalyticsScope.Launch(_analytics.FileIODispatcher, async () =>
        {
            while (!_writeChannel.isCancelled)
            {
                RawEvent e = await _writeChannel.Receive();
                bool isPoison = e == s_flushEvent;

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
                    _uploadChannel.Send(UploadSig);
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
                bool reportFlushComplete = _flushCompleteChannel != null;
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
                if(reportFlushComplete && _flushCompleteChannel != null)
                {
                    _flushCompleteChannel.Send(FlushCompleteSig);
                }
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
