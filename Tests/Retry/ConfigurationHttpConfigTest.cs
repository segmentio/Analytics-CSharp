using System;
using Moq;
using Segment.Analytics;
using Segment.Analytics.Retry;
using Segment.Analytics.Utilities;
using Segment.Serialization;
using Tests.Utils;
using Xunit;

namespace Tests.Retry
{
    /// <summary>
    /// Configuration.HttpConfig is the user-facing entry point for retry settings,
    /// mirroring Kotlin's Configuration.httpConfig and Swift's .httpConfig(_:).
    /// These cover that a config supplied there actually reaches the pipeline's
    /// retry state machine; CDN settings still override it later via UpdateHttpConfig.
    /// </summary>
    public class ConfigurationHttpConfigTest
    {
        private static Analytics CreateAnalytics(HttpConfig httpConfig)
        {
            Settings? settings = JsonUtility.FromJson<Settings?>(
                "{\"integrations\":{\"Segment.io\":{\"apiKey\":\"k\"}},\"plan\":{},\"edgeFunction\":{}}");

            var mockHttpClient = new Mock<HTTPClient>(null, null, null);
            mockHttpClient.Setup(c => c.Settings()).ReturnsAsync(settings);

            var config = new Configuration(
                writeKey: "123",
                autoAddSegmentDestination: false,
                useSynchronizeDispatcher: true,
                flushInterval: 0,
                flushAt: 2,
                httpClientProvider: new MockHttpClientProvider(mockHttpClient),
                storageProvider: new MockStorageProvider(new Mock<IStorage>())
            )
            {
                HttpConfig = httpConfig
            };
            return new Analytics(config);
        }

        [Fact]
        public void Configuration_ExposesHttpConfig()
        {
            var httpConfig = new HttpConfig(backoffConfig: new BackoffConfig(enabled: true, maxRetryCount: 7));
            Analytics analytics = CreateAnalytics(httpConfig);

            Assert.Same(httpConfig, analytics.Configuration.HttpConfig);
        }

        [Fact]
        public void Configuration_HttpConfigDefaultsToNull()
        {
            Analytics analytics = CreateAnalytics(null);

            Assert.Null(analytics.Configuration.HttpConfig);
        }

        [Fact]
        public void EventPipeline_WithoutHttpConfig_RetriesByDefault()
        {
            // Server-side users get no CDN settings, so a null HttpConfig has to mean
            // "retry with the defaults", not "no retry behavior at all".
            Analytics analytics = CreateAnalytics(null);

            var pipeline = (EventPipeline)new EventPipelineProvider().Create(analytics, "key");

            Assert.False(pipeline._retryStateMachine.IsLegacyMode);
        }

        [Fact]
        public void EventPipeline_WithBothSubsystemsDisabled_IsLegacyMode()
        {
            // Opting out is now explicit rather than the default.
            Analytics analytics = CreateAnalytics(new HttpConfig(
                new RateLimitConfig(enabled: false),
                new BackoffConfig(enabled: false)));

            var pipeline = (EventPipeline)new EventPipelineProvider().Create(analytics, "key");

            Assert.True(pipeline._retryStateMachine.IsLegacyMode);
        }

        [Fact]
        public void EventPipeline_WithHttpConfig_LeavesLegacyMode()
        {
            Analytics analytics = CreateAnalytics(
                new HttpConfig(backoffConfig: new BackoffConfig(enabled: true)));

            var pipeline = (EventPipeline)new EventPipelineProvider().Create(analytics, "key");

            Assert.False(pipeline._retryStateMachine.IsLegacyMode);
        }

        [Fact]
        public void SyncEventPipeline_WithoutHttpConfig_RetriesByDefault()
        {
            Analytics analytics = CreateAnalytics(null);

            var pipeline = (SyncEventPipeline)new SyncEventPipelineProvider().Create(analytics, "key");

            Assert.False(pipeline._retryStateMachine.IsLegacyMode);
        }

        [Fact]
        public void RateLimitCountIsReachedLongBeforeTheDurationBudget()
        {
            // MaxRateLimitDuration is a last-ditch guard, not the working limit: the
            // count is what should stop retrying at the defaults. If this ever inverts,
            // batches start dying on a 12h timer instead of a countable number of tries.
            var rateLimit = new RateLimitConfig();

            long worstCaseSeconds =
                (long)rateLimit.MaxRetryCount * RateLimitConfig.MaxRetryIntervalCeiling;

            Assert.True(
                worstCaseSeconds < rateLimit.MaxRateLimitDuration,
                $"count trips after at most {worstCaseSeconds}s but the duration budget is "
                + $"{rateLimit.MaxRateLimitDuration}s; the duration should never be reached first");
        }

        [Fact]
        public void RetryAfterIsCappedAtFiveMinutes()
        {
            // Other SDKs fix this at 300s; C# allowed configuring up to 3600s.
            var validated = new RateLimitConfig(maxRetryInterval: 3600).Validated();

            Assert.Equal(RateLimitConfig.MaxRetryIntervalCeiling, validated.MaxRetryInterval);
            Assert.Equal(300, validated.MaxRetryInterval);
        }

        [Fact]
        public void DefaultBackoffShapeMatchesTheOtherSdks()
        {
            // java, go, python, ruby and php all default to 10 retries and a 60s ceiling.
            // These were 100 and 300 while retries were off by default; now that they are
            // on, a drift here changes the load every C# client puts on the endpoint.
            var backoff = new BackoffConfig();

            Assert.Equal(10, backoff.MaxRetryCount);
            Assert.Equal(60, backoff.MaxBackoffInterval);
            Assert.Equal(0.5, backoff.BaseBackoffInterval);
            Assert.Equal(43200, backoff.MaxTotalBackoffDuration);
        }

        [Fact]
        public void MaxRetryCountOfZero_DoesNotDropBeforeTheFirstAttempt()
        {
            // ShouldUploadBatch compares a fresh state's counts against MaxRetryCount,
            // so an unfloored 0 dropped every batch without ever sending it.
            var machine = new RetryStateMachine(new RetryConfig(
                new RateLimitConfig(enabled: true, maxRetryCount: 0).Validated(),
                new BackoffConfig(enabled: true, maxRetryCount: 0).Validated()));

            Tuple<UploadDecision, RetryState> decision =
                machine.ShouldUploadBatch(new RetryState(), "b.json");

            Assert.IsType<UploadDecision.ProceedDecision>(decision.Item1);
        }

        [Fact]
        public void BackoffConfig_DoesNotShareTheDefaultOverrideMap()
        {
            var first = new BackoffConfig(enabled: true);
            first.StatusCodeOverrides[500] = RetryBehavior.Drop;

            var second = new BackoffConfig(enabled: true);

            Assert.False(second.StatusCodeOverrides.ContainsKey(500));
            Assert.NotSame(first.StatusCodeOverrides, second.StatusCodeOverrides);
        }

        [Fact]
        public void UserSuppliedHttpConfig_IsValidatedOnTheWayIn()
        {
            // maxRetryInterval: 0 is out of range and must clamp to 1 second, exactly as the
            // CDN path does via HttpConfigParser. Unvalidated it would schedule the retry at
            // currentTime, i.e. no wait at all.
            Analytics analytics = CreateAnalytics(
                new HttpConfig(rateLimitConfig: new RateLimitConfig(enabled: true, maxRetryInterval: 0)));

            var pipeline = (EventPipeline)new EventPipelineProvider().Create(analytics, "key");
            RetryState state = pipeline._retryStateMachine.HandleResponse(
                new RetryState(),
                new ResponseInfo(429, retryAfterSeconds: null, batchFile: "b.json", currentTime: 1000));

            Assert.Equal(2000, state.WaitUntilTime);
        }

        [Fact]
        public void SyncEventPipeline_WithHttpConfig_LeavesLegacyMode()
        {
            Analytics analytics = CreateAnalytics(
                new HttpConfig(rateLimitConfig: new RateLimitConfig(enabled: true)));

            var pipeline = (SyncEventPipeline)new SyncEventPipelineProvider().Create(analytics, "key");

            Assert.False(pipeline._retryStateMachine.IsLegacyMode);
        }
    }
}
