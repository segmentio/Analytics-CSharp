using System.Collections.Generic;
using global::System;
using global::System.Linq;
using Segment.Analytics.Policies;
using Segment.Analytics.Retry;
using Segment.Concurrent;
using Segment.Serialization;

namespace Segment.Analytics.Utilities
{
    public class EventPipeline: IEventPipeline
    {
        private readonly Analytics _analytics;

        private readonly string _logTag;

        private readonly IList<IFlushPolicy> _flushPolicies;

        private Channel<RawEvent> _writeChannel;

        private Channel<string> _uploadChannel;

        internal readonly HTTPClient _httpClient;

        private readonly IStorage _storage;

        // volatile: swapped on AnalyticsDispatcher by UpdateHttpConfig, read on
        // NetworkIODispatcher by Upload — ensures the upload thread sees the latest machine.
        internal volatile RetryStateMachine _retryStateMachine;

        private RetryState _retryState;

        public string ApiHost { get; set; }

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
            : this(analytics, logTag, apiKey, flushPolicies, apiHost, (HttpConfig)null) { }

        internal EventPipeline(
            Analytics analytics,
            string logTag,
            string apiKey,
            IList<IFlushPolicy> flushPolicies,
            string apiHost,
            HttpConfig httpConfig)
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

            var retryConfig = httpConfig != null
                ? new RetryConfig(httpConfig.RateLimitConfig, httpConfig.BackoffConfig)
                : new RetryConfig();
            _retryStateMachine = new RetryStateMachine(retryConfig);
            _retryState = RetryStateStorage.LoadRetryState(_storage);
        }

        internal void UpdateHttpConfig(HttpConfig config)
        {
            var retryConfig = config != null
                ? new RetryConfig(config.RateLimitConfig, config.BackoffConfig)
                : new RetryConfig();
            _retryStateMachine = new RetryStateMachine(retryConfig);
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

                // Snapshot the (volatile) state machine once so a mid-flush UpdateHttpConfig
                // swap can't yield inconsistent decisions across batches in the same cycle.
                RetryStateMachine retryStateMachine = _retryStateMachine;

                string[] fileUrlList = _storage.Read(StorageConstants.Events).Split(',');
                foreach (string url in fileUrlList)
                {
                    if (string.IsNullOrEmpty(url))
                        continue;

                    var decision = retryStateMachine.ShouldUploadBatch(_retryState, url);
                    _retryState = decision.Item2;

                    if (decision.Item1 is UploadDecision.SkipAllBatchesDecision)
                    {
                        Analytics.Logger.Log(LogLevel.Debug, message: _logTag + " skipping uploads: pipeline is rate-limited");
                        break;
                    }
                    if (decision.Item1 is UploadDecision.SkipThisBatchDecision)
                    {
                        Analytics.Logger.Log(LogLevel.Debug, message: _logTag + " skipping batch " + url + ": not ready for retry");
                        continue;
                    }
                    if (decision.Item1 is UploadDecision.DropBatchDecision dropDecision)
                    {
                        Analytics.Logger.Log(LogLevel.Error, message: _logTag + " dropping batch " + url + ": " + dropDecision.Reason);
                        _analytics.ReportInternalError(AnalyticsErrorType.NetworkServerRejected,
                            message: "Batch dropped: " + dropDecision.Reason);
                        _storage.RemoveFile(url);
                        await Scope.WithContext(_analytics.FileIODispatcher, () =>
                            RetryStateStorage.SaveRetryState(_storage, _retryState));
                        continue;
                    }

                    // Proceed with upload
                    byte[] data = _storage.ReadAsBytes(url);
                    if (data == null)
                        continue;

                    int retryCount = retryStateMachine.GetRetryCount(_retryState, url);
                    int statusCode = 0;
                    int? retryAfterSeconds = null;
                    bool shouldCleanup = true;

                    try
                    {
                        HTTPClient.Response response = await _httpClient.UploadWithResponse(data, retryCount);
                        statusCode = response.StatusCode;

                        if (!string.IsNullOrEmpty(response.RetryAfterHeader)
                            && int.TryParse(response.RetryAfterHeader.Trim(), out int parsedRetryAfter))
                        {
                            retryAfterSeconds = parsedRetryAfter;
                        }

                        if (response.IsSuccessStatusCode)
                        {
                            Analytics.Logger.Log(LogLevel.Debug, message: _logTag + " uploaded " + url);
                            shouldCleanup = true;
                        }
                        else
                        {
                            Analytics.Logger.Log(LogLevel.Error, message: "Error " + statusCode + " uploading " + url);
                            shouldCleanup = _retryStateMachine.ShouldDeleteBatch(statusCode);
                            if (shouldCleanup)
                            {
                                _analytics.ReportInternalError(AnalyticsErrorType.NetworkServerRejected,
                                    message: "HTTP " + statusCode + ": batch rejected by server");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Analytics.Logger.Log(LogLevel.Error, e, _logTag + ": Error uploading to url");
                        statusCode = 0;
                        shouldCleanup = false;
                    }

                    // Update retry state based on response
                    var responseInfo = new ResponseInfo(
                        statusCode: statusCode > 0 ? statusCode : 500,
                        retryAfterSeconds: retryAfterSeconds,
                        batchFile: url,
                        currentTime: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    );
                    _retryState = retryStateMachine.HandleResponse(_retryState, responseInfo);
                    await Scope.WithContext(_analytics.FileIODispatcher, () =>
                        RetryStateStorage.SaveRetryState(_storage, _retryState));

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
