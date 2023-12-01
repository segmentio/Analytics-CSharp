using System.Collections.Generic;
using global::System;
using global::System.Linq;
using Segment.Analytics.Policies;
using Segment.Concurrent;
using Segment.Serialization;

namespace Segment.Analytics.Utilities
{
    internal class EventPipeline
    {
        private readonly Analytics _analytics;

        private readonly string _logTag;

        private readonly IList<IFlushPolicy> _flushPolicies;

        private Channel<RawEvent> _writeChannel;

        private Channel<string> _uploadChannel;

        private readonly HTTPClient _httpClient;

        private readonly IStorage _storage;

        internal string ApiHost { get; set; }

        public bool Running { get; private set; }

        internal const string FlushPoison = "#!flush";

        internal static readonly RawEvent s_flushEvent = new ScreenEvent(FlushPoison, FlushPoison);

        internal const string UploadSig = "#!upload";

        public EventPipeline(
            Analytics analytics,
            string logTag,
            string apiKey,
            IList<IFlushPolicy> flushPolicies,
            string apiHost = HTTPClient.DefaultAPIHost)
        {
            _analytics = analytics;
            _logTag = logTag;
            _flushPolicies = flushPolicies;
            ApiHost = apiHost;

            _writeChannel = new Channel<RawEvent>();
            _uploadChannel = new Channel<string>();
            _httpClient = analytics.Configuration.HttpClientProvider.CreateHTTPClient(apiKey, apiHost: apiHost);
            _httpClient.AnalyticsRef = analytics;
            _storage = analytics.Storage;
            Running = false;
        }

        public void Put(RawEvent @event) => _writeChannel.Send(@event);

        public void Flush() => _writeChannel.Send(s_flushEvent);

        public void Start()
        {
            if (Running) return;

            // avoid to re-establish a channel if the pipeline just gets created
            if (_writeChannel.isCancelled)
            {
                _writeChannel = new Channel<RawEvent>();
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
