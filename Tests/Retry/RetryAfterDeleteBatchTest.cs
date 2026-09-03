using Segment.Analytics.Retry;
using Xunit;

namespace Tests.Retry
{
    /// <summary>
    /// Regression: a retryable status carrying Retry-After routes to the rate-limit path,
    /// so the batch must NOT also be deleted — otherwise the pipeline stalls waiting to
    /// retry a batch that no longer exists. Mirrors swift's shouldDropBatch, which returns
    /// false for retryable codes whenever rate limiting is enabled.
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
        public void RetryableStatus_WithRateLimitEnabled_IsNotDeleted(int status)
        {
            Assert.False(RateLimitOnlyMachine().ShouldDeleteBatch(status));
        }

        [Fact]
        public void RetryAfter_RateLimitsPipelineAndKeepsBatch()
        {
            var machine = RateLimitOnlyMachine();
            var response = new ResponseInfo(503, retryAfterSeconds: 30, batchFile: "b.json", currentTime: 1000);

            RetryState state = machine.HandleResponse(new RetryState(), response);

            Assert.Equal(PipelineState.RateLimited, state.PipelineState);
            Assert.Equal(31000, state.WaitUntilTime);
            Assert.False(machine.ShouldDeleteBatch(503));
        }

        [Fact]
        public void NonRetryableStatus_IsStillDeleted()
        {
            Assert.True(RateLimitOnlyMachine().ShouldDeleteBatch(400));
            Assert.True(RateLimitOnlyMachine().ShouldDeleteBatch(501));
        }
    }
}
