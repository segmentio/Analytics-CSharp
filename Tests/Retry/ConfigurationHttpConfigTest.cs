using System;
using System.Collections.Generic;
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
        public void StatusCodeOverridesMergeOverTheDefaultsRatherThanReplacingThem()
        {
            // Overriding one status used to drop the defaults for every other: 408,
            // 410, 429 and 460 stopped being retried, and 511 fell through to
            // Default5xxBehavior and started being retried.
            var config = new BackoffConfig(
                statusCodeOverrides: new Dictionary<int, RetryBehavior>
                {
                    { 503, RetryBehavior.Drop }
                });

            Assert.Equal(RetryBehavior.Drop, config.StatusCodeOverrides[503]);
            Assert.Equal(RetryBehavior.Retry, config.StatusCodeOverrides[408]);
            Assert.Equal(RetryBehavior.Retry, config.StatusCodeOverrides[410]);
            Assert.Equal(RetryBehavior.Retry, config.StatusCodeOverrides[429]);
            Assert.Equal(RetryBehavior.Retry, config.StatusCodeOverrides[460]);
            Assert.Equal(RetryBehavior.Drop, config.StatusCodeOverrides[501]);
            Assert.Equal(RetryBehavior.Drop, config.StatusCodeOverrides[505]);
            Assert.Equal(RetryBehavior.Drop, config.StatusCodeOverrides[511]);
        }

        [Fact]
        public void AnOverrideCanStillContradictADefault()
        {
            // Merging must not make the defaults unopposable.
            var config = new BackoffConfig(
                statusCodeOverrides: new Dictionary<int, RetryBehavior>
                {
                    { 429, RetryBehavior.Drop }
                });

            Assert.Equal(RetryBehavior.Drop, config.StatusCodeOverrides[429]);
        }

        [Fact]
        public void MaxTotalBackoffDurationOfZeroDoesNotAbandonOnTheSecondAttempt()
        {
            // ExceedsMaxDuration compares elapsed time against this, so an unfloored
            // 0 read as "no budget" rather than "no cap".
            var validated = new BackoffConfig(maxTotalBackoffDuration: 0).Validated();

            Assert.True(validated.MaxTotalBackoffDuration >= 1);
        }

        [Fact]
        public void WhichRateLimitBoundBindsDependsOnTheRetryAfterBeingServed()
        {
            // The previous version of this test asserted the duration was below the
            // count's theoretical maximum span (100 x 300s), which is true and says
            // nothing: it passes while the count is the limit actually reached. The
            // crossover is what matters, and at the defaults it sits at 18 seconds --
            // so the count, not the duration, bounds every episode whose Retry-After
            // is shorter than that, which is most of them.
            var rateLimit = new RateLimitConfig();

            double crossoverSeconds =
                (double)rateLimit.MaxRateLimitDuration / rateLimit.MaxRetryCount;

            // Below the crossover the count runs out first.
            Assert.True(
                rateLimit.MaxRetryCount * (crossoverSeconds / 2) < rateLimit.MaxRateLimitDuration,
                "expected the count to bind for a Retry-After below the crossover");

            // Above it the duration does.
            Assert.True(
                rateLimit.MaxRetryCount * (crossoverSeconds * 2) > rateLimit.MaxRateLimitDuration,
                "expected the duration to bind for a Retry-After above the crossover");

            // Documented so a change to either constant has to restate it.
            Assert.Equal(18.0, crossoverSeconds);
        }

        [Fact]
        public void DefaultBudgetLeavesRoomForMoreThanOneMaximalWait()
        {
            // At parity the rate-limit path performs no retries at all. A response with
            // no usable Retry-After waits MaxRetryInterval by default, ShouldUploadBatch
            // tests elapsed time before the wait, so that one wait spends the budget and
            // the next evaluation drops the batch — one attempt, having stalled the whole
            // pipeline for the duration first.
            var rateLimit = new RateLimitConfig();

            Assert.True(
                rateLimit.MaxRateLimitDuration > rateLimit.MaxRetryInterval,
                $"budget is {rateLimit.MaxRateLimitDuration}s against a {rateLimit.MaxRetryInterval}s "
                + "interval; a single maximal wait would consume it and leave no retry");

            Assert.True(
                rateLimit.MaxRateLimitDuration / rateLimit.MaxRetryInterval >= 2,
                "budget leaves room for fewer than two maximal waits");
        }

        [Fact]
        public void RetryAfterCeilingClampsAnAbsurdValue()
        {
            // The ceiling exists to reject a nonsense header, not to shorten a
            // reasonable one — a value inside it is honoured as given, because waiting
            // less than asked only adds requests against a server already rate-limiting
            // us. MaxRateLimitDuration is what bounds how long we keep trying.
            Assert.Equal(
                RateLimitConfig.MaxRetryIntervalCeiling,
                new RateLimitConfig(maxRetryInterval: 3600).Validated().MaxRetryInterval);

            Assert.Equal(120, new RateLimitConfig(maxRetryInterval: 120).Validated().MaxRetryInterval);
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
