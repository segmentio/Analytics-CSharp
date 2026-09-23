using Segment.Analytics.Retry;
using Xunit;

namespace Tests.Retry
{
    /// <summary>
    /// ShouldDeleteBatch must agree with HandleResponse about whether a response took the
    /// rate-limit path. A retryable status carrying Retry-After schedules a retry, so its
    /// batch must be kept; without Retry-After only backoff can retry it.
    /// </summary>
    public class RetryAfterDeleteBatchTest
    {
        private static RetryStateMachine RateLimitOnlyMachine() =>
            new RetryStateMachine(new RetryConfig(
                new RateLimitConfig(enabled: true),
                new BackoffConfig(enabled: false)));

        [Theory]
        [InlineData(503)]
        [InlineData(529)]
        [InlineData(408)]
        [InlineData(410)]
        public void RetryableStatus_WithRetryAfter_IsKept(int status)
        {
            Assert.False(RateLimitOnlyMachine().ShouldDeleteBatch(status, 30));
        }

        [Theory]
        [InlineData(503)]
        [InlineData(529)]
        public void RetryableStatus_WithoutRetryAfter_AndBackoffDisabled_IsDeleted(int status)
        {
            // Nothing would retry it, so holding the file would leak storage.
            Assert.True(RateLimitOnlyMachine().ShouldDeleteBatch(status, null));
        }

        [Fact]
        public void RetryAfterZero_DoesNotCountAsRateLimited()
        {
            Assert.True(RateLimitOnlyMachine().ShouldDeleteBatch(503, 0));
        }

        [Fact]
        public void RetryAfter_RateLimitsPipelineAndKeepsBatch()
        {
            var machine = RateLimitOnlyMachine();
            var response = new ResponseInfo(503, retryAfterSeconds: 30, batchFile: "b.json", currentTime: 1000);

            RetryState state = machine.HandleResponse(new RetryState(), response);

            Assert.Equal(PipelineState.RateLimited, state.PipelineState);
            Assert.Equal(31000, state.WaitUntilTime);
            Assert.False(machine.ShouldDeleteBatch(503, 30));
        }

        [Theory]
        [InlineData(200)]
        [InlineData(201)]
        [InlineData(204)]
        public void SuccessStatuses_AreDeleted(int status)
        {
            // 2xx is success, so the batch is done with.
            Assert.True(RateLimitOnlyMachine().ShouldDeleteBatch(status, null));
        }

        [Theory]
        [InlineData(300)]
        [InlineData(301)]
        [InlineData(304)]
        public void Redirects_AreNotSuccess(int status)
        {
            // HttpClient follows what it can; a 3xx arriving here means nothing was
            // uploaded, so the batch must not be treated as delivered.
            var machine = RateLimitOnlyMachine();
            RetryState state = machine.HandleResponse(
                new RetryState(),
                new ResponseInfo(status, retryAfterSeconds: null, batchFile: "b.json", currentTime: 1000));

            Assert.Equal(PipelineState.Ready, state.PipelineState);
            Assert.True(machine.ShouldDeleteBatch(status, null));
        }

        [Fact]
        public void NonRetryableStatus_IsDeletedEvenWithRetryAfter()
        {
            Assert.True(RateLimitOnlyMachine().ShouldDeleteBatch(400, 30));
            Assert.True(RateLimitOnlyMachine().ShouldDeleteBatch(501, 30));
        }

        [Fact]
        public void NetworkAuthenticationRequired_IsDropped()
        {
            // 511 is retryable only for an SDK that can re-authenticate; this one cannot.
            Assert.True(RateLimitOnlyMachine().ShouldDeleteBatch(511, null));
            Assert.True(RateLimitOnlyMachine().ShouldDeleteBatch(511, 30));
        }
    }
}
