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
                storageProvider: new MockStorageProvider(new Mock<IStorage>()),
                httpConfig: httpConfig
            );
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
        public void EventPipeline_WithoutHttpConfig_IsLegacyMode()
        {
            Analytics analytics = CreateAnalytics(null);

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
        public void SyncEventPipeline_WithoutHttpConfig_IsLegacyMode()
        {
            Analytics analytics = CreateAnalytics(null);

            var pipeline = (SyncEventPipeline)new SyncEventPipelineProvider().Create(analytics, "key");

            Assert.True(pipeline._retryStateMachine.IsLegacyMode);
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
