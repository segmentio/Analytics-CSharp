using System;
using Segment.Analytics.Retry;
using Xunit;

namespace Tests.Retry
{
    public class FakeTimeProvider : ITimeProvider
    {
        public long Time { get; set; }
        public long CurrentTimeMillis() => Time;
    }

    public class RetryStateMachineTest
    {
        private RetryStateMachine CreateMachine(
            bool rateLimitEnabled = true,
            bool backoffEnabled = true,
            int maxRetryCount = 100,
            int maxRetryInterval = 300,
            FakeTimeProvider timeProvider = null)
        {
            var config = new RetryConfig(
                new RateLimitConfig(enabled: rateLimitEnabled, maxRetryCount: maxRetryCount, maxRetryInterval: maxRetryInterval),
                new BackoffConfig(enabled: backoffEnabled, maxRetryCount: maxRetryCount)
            );
            return new RetryStateMachine(config, timeProvider ?? new FakeTimeProvider(), new Random(42));
        }

        [Fact]
        public void LegacyMode_BothDisabled()
        {
            var machine = CreateMachine(rateLimitEnabled: false, backoffEnabled: false);
            Assert.True(machine.IsLegacyMode);
        }

        [Fact]
        public void NotLegacyMode_WhenEitherEnabled()
        {
            var machine = CreateMachine(rateLimitEnabled: true, backoffEnabled: false);
            Assert.False(machine.IsLegacyMode);
        }

        // --- HandleResponse tests ---

        [Fact]
        public void HandleResponse_Success_ClearsState()
        {
            var machine = CreateMachine();
            var state = new RetryState(globalRetryCount: 5);
            var response = new ResponseInfo(200, null, "batch1.json", 1000);

            RetryState newState = machine.HandleResponse(state, response);

            Assert.Equal(PipelineState.Ready, newState.PipelineState);
            Assert.Equal(0, newState.GlobalRetryCount);
            Assert.Null(newState.WaitUntilTime);
            Assert.False(newState.BatchMetadata.ContainsKey("batch1.json"));
        }

        [Fact]
        public void HandleResponse_429_SetsRateLimited()
        {
            var machine = CreateMachine(maxRetryInterval: 300);
            var state = new RetryState();
            var response = new ResponseInfo(429, 60, "batch1.json", 1000);

            RetryState newState = machine.HandleResponse(state, response);

            Assert.Equal(PipelineState.RateLimited, newState.PipelineState);
            Assert.Equal(1, newState.GlobalRetryCount);
            Assert.Equal(61000L, newState.WaitUntilTime); // 1000 + 60*1000
        }

        [Fact]
        public void HandleResponse_429_ClampsRetryAfter()
        {
            var machine = CreateMachine(maxRetryInterval: 10);
            var state = new RetryState();
            var response = new ResponseInfo(429, 999, "batch1.json", 1000);

            RetryState newState = machine.HandleResponse(state, response);

            // Clamped to maxRetryInterval=10
            Assert.Equal(11000L, newState.WaitUntilTime); // 1000 + 10*1000
        }

        [Fact]
        public void HandleResponse_429_NullRetryAfter_UsesMaxInterval()
        {
            var machine = CreateMachine(maxRetryInterval: 300);
            var state = new RetryState();
            var response = new ResponseInfo(429, null, "batch1.json", 1000);

            RetryState newState = machine.HandleResponse(state, response);

            Assert.Equal(301000L, newState.WaitUntilTime); // 1000 + 300*1000
        }

        [Fact]
        public void HandleResponse_429_RateLimitDisabled_DropsBatch()
        {
            var machine = CreateMachine(rateLimitEnabled: false, backoffEnabled: true);
            var state = new RetryState(
                batchMetadata: new System.Collections.Generic.Dictionary<string, BatchMetadata>
                {
                    { "batch1.json", new BatchMetadata(failureCount: 1) }
                });
            var response = new ResponseInfo(429, 60, "batch1.json", 1000);

            RetryState newState = machine.HandleResponse(state, response);

            Assert.False(newState.BatchMetadata.ContainsKey("batch1.json"));
        }

        [Fact]
        public void HandleResponse_500_TracksBackoff()
        {
            var machine = CreateMachine();
            var state = new RetryState();
            var response = new ResponseInfo(500, null, "batch1.json", 1000);

            RetryState newState = machine.HandleResponse(state, response);

            Assert.True(newState.BatchMetadata.ContainsKey("batch1.json"));
            Assert.Equal(1, newState.BatchMetadata["batch1.json"].FailureCount);
            Assert.Equal(1000L, newState.BatchMetadata["batch1.json"].FirstFailureTime);
            Assert.NotNull(newState.BatchMetadata["batch1.json"].NextRetryTime);
        }

        [Fact]
        public void HandleResponse_500_IncreasesBackoff()
        {
            var machine = CreateMachine();
            var existing = new BatchMetadata(failureCount: 2, firstFailureTime: 0, nextRetryTime: 500);
            var state = new RetryState(
                batchMetadata: new System.Collections.Generic.Dictionary<string, BatchMetadata>
                {
                    { "batch1.json", existing }
                });
            var response = new ResponseInfo(500, null, "batch1.json", 5000);

            RetryState newState = machine.HandleResponse(state, response);

            Assert.Equal(3, newState.BatchMetadata["batch1.json"].FailureCount);
            Assert.True(newState.BatchMetadata["batch1.json"].NextRetryTime > 5000);
        }

        [Fact]
        public void HandleResponse_400_DropsBatch()
        {
            var machine = CreateMachine();
            var state = new RetryState(
                batchMetadata: new System.Collections.Generic.Dictionary<string, BatchMetadata>
                {
                    { "batch1.json", new BatchMetadata(failureCount: 1) }
                });
            var response = new ResponseInfo(400, null, "batch1.json", 1000);

            RetryState newState = machine.HandleResponse(state, response);

            Assert.False(newState.BatchMetadata.ContainsKey("batch1.json"));
        }

        [Fact]
        public void HandleResponse_501_DropsBatch()
        {
            var machine = CreateMachine();
            var state = new RetryState();
            var response = new ResponseInfo(501, null, "batch1.json", 1000);

            RetryState newState = machine.HandleResponse(state, response);

            Assert.False(newState.BatchMetadata.ContainsKey("batch1.json"));
        }

        [Fact]
        public void HandleResponse_408_RetriesWithBackoff()
        {
            var machine = CreateMachine();
            var state = new RetryState();
            var response = new ResponseInfo(408, null, "batch1.json", 1000);

            RetryState newState = machine.HandleResponse(state, response);

            Assert.True(newState.BatchMetadata.ContainsKey("batch1.json"));
            Assert.Equal(1, newState.BatchMetadata["batch1.json"].FailureCount);
        }

        // --- Legacy mode tests ---

        [Fact]
        public void HandleResponse_LegacyMode_429_KeepsBatch()
        {
            var machine = CreateMachine(rateLimitEnabled: false, backoffEnabled: false);
            var state = new RetryState();
            var response = new ResponseInfo(429, null, "batch1.json", 1000);

            RetryState newState = machine.HandleResponse(state, response);

            Assert.Same(state, newState); // unchanged
        }

        [Fact]
        public void HandleResponse_LegacyMode_500_KeepsBatch()
        {
            var machine = CreateMachine(rateLimitEnabled: false, backoffEnabled: false);
            var state = new RetryState();
            var response = new ResponseInfo(500, null, "batch1.json", 1000);

            RetryState newState = machine.HandleResponse(state, response);

            Assert.Same(state, newState);
        }

        [Fact]
        public void HandleResponse_LegacyMode_400_DropsBatch()
        {
            var machine = CreateMachine(rateLimitEnabled: false, backoffEnabled: false);
            var state = new RetryState(
                batchMetadata: new System.Collections.Generic.Dictionary<string, BatchMetadata>
                {
                    { "batch1.json", new BatchMetadata(failureCount: 1) }
                });
            var response = new ResponseInfo(400, null, "batch1.json", 1000);

            RetryState newState = machine.HandleResponse(state, response);

            Assert.False(newState.BatchMetadata.ContainsKey("batch1.json"));
        }

        // --- ShouldUploadBatch tests ---

        [Fact]
        public void ShouldUploadBatch_LegacyMode_AlwaysProceeds()
        {
            var machine = CreateMachine(rateLimitEnabled: false, backoffEnabled: false);
            var state = new RetryState();

            var result = machine.ShouldUploadBatch(state, "batch1.json");

            Assert.IsType<UploadDecision.ProceedDecision>(result.Item1);
        }

        [Fact]
        public void ShouldUploadBatch_RateLimited_SkipsAll()
        {
            var tp = new FakeTimeProvider { Time = 5000 };
            var machine = CreateMachine(timeProvider: tp);
            var state = new RetryState(
                pipelineState: PipelineState.RateLimited,
                waitUntilTime: 10000);

            var result = machine.ShouldUploadBatch(state, "batch1.json");

            Assert.IsType<UploadDecision.SkipAllBatchesDecision>(result.Item1);
        }

        [Fact]
        public void ShouldUploadBatch_RateLimitExpired_Proceeds()
        {
            var tp = new FakeTimeProvider { Time = 15000 };
            var machine = CreateMachine(timeProvider: tp);
            var state = new RetryState(
                pipelineState: PipelineState.RateLimited,
                waitUntilTime: 10000);

            var result = machine.ShouldUploadBatch(state, "batch1.json");

            Assert.IsType<UploadDecision.ProceedDecision>(result.Item1);
            Assert.Equal(PipelineState.Ready, result.Item2.PipelineState);
        }

        [Fact]
        public void ShouldUploadBatch_MaxRetriesExceeded_DropsBatch()
        {
            var tp = new FakeTimeProvider { Time = 1000 };
            var machine = CreateMachine(maxRetryCount: 3, timeProvider: tp);
            var state = new RetryState(
                batchMetadata: new System.Collections.Generic.Dictionary<string, BatchMetadata>
                {
                    { "batch1.json", new BatchMetadata(failureCount: 3, nextRetryTime: 0, firstFailureTime: 0) }
                });

            var result = machine.ShouldUploadBatch(state, "batch1.json");

            Assert.IsType<UploadDecision.DropBatchDecision>(result.Item1);
            Assert.Equal(DropReason.MaxRetriesExceeded,
                ((UploadDecision.DropBatchDecision)result.Item1).Reason);
        }

        [Fact]
        public void ShouldUploadBatch_BackoffNotReady_Skips()
        {
            var tp = new FakeTimeProvider { Time = 1000 };
            var machine = CreateMachine(timeProvider: tp);
            var state = new RetryState(
                batchMetadata: new System.Collections.Generic.Dictionary<string, BatchMetadata>
                {
                    { "batch1.json", new BatchMetadata(failureCount: 1, nextRetryTime: 5000, firstFailureTime: 0) }
                });

            var result = machine.ShouldUploadBatch(state, "batch1.json");

            Assert.IsType<UploadDecision.SkipThisBatchDecision>(result.Item1);
        }

        [Fact]
        public void ShouldUploadBatch_BackoffReady_Proceeds()
        {
            var tp = new FakeTimeProvider { Time = 6000 };
            var machine = CreateMachine(timeProvider: tp);
            var state = new RetryState(
                batchMetadata: new System.Collections.Generic.Dictionary<string, BatchMetadata>
                {
                    { "batch1.json", new BatchMetadata(failureCount: 1, nextRetryTime: 5000, firstFailureTime: 0) }
                });

            var result = machine.ShouldUploadBatch(state, "batch1.json");

            Assert.IsType<UploadDecision.ProceedDecision>(result.Item1);
        }

        // --- ShouldDeleteBatch tests ---

        [Fact]
        public void ShouldDeleteBatch_LegacyMode_400_True()
        {
            var machine = CreateMachine(rateLimitEnabled: false, backoffEnabled: false);
            Assert.True(machine.ShouldDeleteBatch(400));
        }

        [Fact]
        public void ShouldDeleteBatch_LegacyMode_429_False()
        {
            var machine = CreateMachine(rateLimitEnabled: false, backoffEnabled: false);
            Assert.False(machine.ShouldDeleteBatch(429));
        }

        [Fact]
        public void ShouldDeleteBatch_LegacyMode_500_False()
        {
            var machine = CreateMachine(rateLimitEnabled: false, backoffEnabled: false);
            Assert.False(machine.ShouldDeleteBatch(500));
        }

        [Fact]
        public void ShouldDeleteBatch_SmartMode_400_True()
        {
            var machine = CreateMachine();
            Assert.True(machine.ShouldDeleteBatch(400));
        }

        [Fact]
        public void ShouldDeleteBatch_SmartMode_500_False()
        {
            var machine = CreateMachine();
            Assert.False(machine.ShouldDeleteBatch(500));
        }

        [Fact]
        public void ShouldDeleteBatch_SmartMode_501_True()
        {
            var machine = CreateMachine();
            Assert.True(machine.ShouldDeleteBatch(501));
        }

        [Fact]
        public void ShouldDeleteBatch_SmartMode_408_False()
        {
            var machine = CreateMachine();
            Assert.False(machine.ShouldDeleteBatch(408));
        }

        // --- RetryAfterSeconds on retryable errors ---

        [Fact]
        public void HandleResponse_503_WithRetryAfter_RoutesToRateLimitPath()
        {
            var machine = CreateMachine(maxRetryInterval: 300);
            var state = new RetryState();
            var response = new ResponseInfo(503, retryAfterSeconds: 2, batchFile: "batch1.json", currentTime: 1000);

            RetryState newState = machine.HandleResponse(state, response);

            Assert.Equal(PipelineState.RateLimited, newState.PipelineState);
            Assert.Equal(1, newState.GlobalRetryCount);
            Assert.Equal(1000L + 2000L, newState.WaitUntilTime);
        }

        [Fact]
        public void HandleResponse_529_WithRetryAfter_RoutesToRateLimitPath()
        {
            var machine = CreateMachine(maxRetryInterval: 300);
            var state = new RetryState();
            var response = new ResponseInfo(529, retryAfterSeconds: 3, batchFile: "batch1.json", currentTime: 1000);

            RetryState newState = machine.HandleResponse(state, response);

            Assert.Equal(PipelineState.RateLimited, newState.PipelineState);
            Assert.Equal(1, newState.GlobalRetryCount);
            Assert.Equal(1000L + 3000L, newState.WaitUntilTime);
        }

        [Fact]
        public void HandleResponse_503_WithoutRetryAfter_UsesExponentialBackoff()
        {
            var machine = CreateMachine();
            var state = new RetryState();
            var response = new ResponseInfo(503, retryAfterSeconds: null, batchFile: "batch1.json", currentTime: 1000);

            RetryState newState = machine.HandleResponse(state, response);

            // Still goes through backoff path (failureCount incremented, not rate-limited)
            Assert.True(newState.BatchMetadata.ContainsKey("batch1.json"));
            Assert.Equal(1, newState.BatchMetadata["batch1.json"].FailureCount);
            Assert.True(newState.BatchMetadata["batch1.json"].NextRetryTime > 1000L);
            Assert.Equal(PipelineState.Ready, newState.PipelineState);
            Assert.Equal(0, newState.GlobalRetryCount);
        }

        [Fact]
        public void HandleResponse_503_WithRetryAfter_ClampsToMaxRetryInterval()
        {
            var config = new RetryConfig(
                new RateLimitConfig(enabled: true, maxRetryCount: 100, maxRetryInterval: 10),
                new BackoffConfig(enabled: true, maxRetryCount: 100, maxBackoffInterval: 300)
            );
            var machine = new RetryStateMachine(config, new FakeTimeProvider(), new Random(42));
            var state = new RetryState();
            var response = new ResponseInfo(503, retryAfterSeconds: 999, batchFile: "batch1.json", currentTime: 1000);

            RetryState newState = machine.HandleResponse(state, response);

            // Now routes through rate-limit path, clamped to maxRetryInterval=10
            Assert.Equal(PipelineState.RateLimited, newState.PipelineState);
            Assert.Equal(1000L + 10 * 1000L, newState.WaitUntilTime);
            Assert.Equal(1, newState.GlobalRetryCount);
        }

        // --- GetRetryCount tests ---

        [Fact]
        public void GetRetryCount_NoMetadata_ReturnsZero()
        {
            var machine = CreateMachine();
            var state = new RetryState();

            Assert.Equal(0, machine.GetRetryCount(state, "batch1.json"));
        }

        [Fact]
        public void GetRetryCount_WithMetadata_ReturnsMax()
        {
            var machine = CreateMachine();
            var state = new RetryState(
                globalRetryCount: 2,
                batchMetadata: new System.Collections.Generic.Dictionary<string, BatchMetadata>
                {
                    { "batch1.json", new BatchMetadata(failureCount: 5) }
                });

            Assert.Equal(5, machine.GetRetryCount(state, "batch1.json"));
        }

        [Fact]
        public void GetRetryCount_GlobalHigher_ReturnsGlobal()
        {
            var machine = CreateMachine();
            var state = new RetryState(
                globalRetryCount: 10,
                batchMetadata: new System.Collections.Generic.Dictionary<string, BatchMetadata>
                {
                    { "batch1.json", new BatchMetadata(failureCount: 2) }
                });

            Assert.Equal(10, machine.GetRetryCount(state, "batch1.json"));
        }
    }
}
