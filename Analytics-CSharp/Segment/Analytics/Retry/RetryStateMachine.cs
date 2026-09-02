using System;
using System.Collections.Generic;

namespace Segment.Analytics.Retry
{
    internal class RetryStateMachine
    {
        private readonly RetryConfig _config;
        private readonly ITimeProvider _timeProvider;
        private readonly Random _random;

        public bool IsLegacyMode => !_config.RateLimitConfig.Enabled && !_config.BackoffConfig.Enabled;

        public RetryStateMachine(RetryConfig config, ITimeProvider timeProvider = null, Random random = null)
        {
            _config = config ?? new RetryConfig();
            _timeProvider = timeProvider ?? new SystemTimeProvider();
            _random = random ?? new Random();
        }

        public RetryState HandleResponse(RetryState state, ResponseInfo response)
        {
            if (IsLegacyMode)
            {
                if (response.StatusCode >= 200 && response.StatusCode <= 299)
                    return state.RemoveBatch(response.BatchFile);
                if (response.StatusCode == 429 || (response.StatusCode >= 500 && response.StatusCode <= 599))
                    return state; // Keep
                return state.RemoveBatch(response.BatchFile); // Drop on 4xx
            }

            long currentTime = response.CurrentTime;

            if (response.StatusCode >= 200 && response.StatusCode <= 299)
            {
                return state.With(
                    pipelineState: PipelineState.Ready,
                    clearWaitUntilTime: true,
                    globalRetryCount: 0,
                    batchMetadata: RemoveFromMetadata(state, response.BatchFile)
                );
            }

            if (response.StatusCode == 429)
            {
                if (_config.RateLimitConfig.Enabled)
                    return HandleRateLimitResponse(state, response, currentTime);
                return state.RemoveBatch(response.BatchFile);
            }

            RetryBehavior behavior = ResolveStatusCodeBehavior(response.StatusCode);
            if (behavior == RetryBehavior.Retry && _config.BackoffConfig.Enabled)
                return HandleRetryableError(state, response, currentTime);

            return state.RemoveBatch(response.BatchFile);
        }

        public Tuple<UploadDecision, RetryState> ShouldUploadBatch(RetryState state, string batchFile)
        {
            if (IsLegacyMode)
                return Tuple.Create(UploadDecision.Proceed, state);

            long currentTime = _timeProvider.CurrentTimeMillis();

            // Check 1: Global rate limiting
            if (state.IsRateLimited(currentTime))
                return Tuple.Create(UploadDecision.SkipAllBatches, state);

            // Clear stale rate limit state if it has expired
            RetryState clearedState = state;
            if (state.PipelineState == PipelineState.RateLimited
                && state.WaitUntilTime != null
                && currentTime >= state.WaitUntilTime.Value)
            {
                clearedState = state.With(
                    pipelineState: PipelineState.Ready,
                    clearWaitUntilTime: true
                );
            }

            // Check 2: Global rate limit retry count
            if (_config.RateLimitConfig.Enabled
                && clearedState.GlobalRetryCount >= _config.RateLimitConfig.MaxRetryCount)
            {
                RetryState resetState = clearedState
                    .With(globalRetryCount: 0)
                    .RemoveBatch(batchFile);
                return Tuple.Create(
                    UploadDecision.DropBatch(DropReason.MaxRetriesExceeded),
                    resetState);
            }

            // Check 3: Per-batch metadata
            BatchMetadata metadata;
            if (clearedState.BatchMetadata.TryGetValue(batchFile, out metadata))
            {
                // Check retry count limit
                if (_config.BackoffConfig.Enabled
                    && metadata.FailureCount >= _config.BackoffConfig.MaxRetryCount)
                {
                    return Tuple.Create(
                        UploadDecision.DropBatch(DropReason.MaxRetriesExceeded),
                        clearedState.RemoveBatch(batchFile));
                }

                // Check duration limit
                if (_config.BackoffConfig.Enabled
                    && metadata.ExceedsMaxDuration(currentTime, _config.BackoffConfig.MaxTotalBackoffDuration * 1000))
                {
                    return Tuple.Create(
                        UploadDecision.DropBatch(DropReason.MaxDurationExceeded),
                        clearedState.RemoveBatch(batchFile));
                }

                // Check if backoff time has passed
                if (_config.BackoffConfig.Enabled && !metadata.ShouldRetry(currentTime))
                {
                    return Tuple.Create(UploadDecision.SkipThisBatch, clearedState);
                }
            }

            return Tuple.Create(UploadDecision.Proceed, clearedState);
        }

        public int GetRetryCount(RetryState state, string batchFile)
        {
            BatchMetadata metadata;
            int batchRetryCount = state.BatchMetadata.TryGetValue(batchFile, out metadata)
                ? metadata.FailureCount
                : 0;
            return Math.Max(batchRetryCount, state.GlobalRetryCount);
        }

        public bool ShouldDeleteBatch(int statusCode)
        {
            if (IsLegacyMode)
                return statusCode >= 400 && statusCode <= 499 && statusCode != 429;

            if (statusCode >= 200 && statusCode <= 299)
                return true;

            if (statusCode == 429)
                return !_config.RateLimitConfig.Enabled;

            RetryBehavior behavior = ResolveStatusCodeBehavior(statusCode);
            if (behavior == RetryBehavior.Retry && !_config.BackoffConfig.Enabled)
                return true;

            return behavior == RetryBehavior.Drop;
        }

        private RetryState HandleRateLimitResponse(RetryState state, ResponseInfo response, long currentTime)
        {
            long waitUntilTimeMs = CalculateWaitUntilTimeMs(response.RetryAfterSeconds, currentTime);
            return state.With(
                pipelineState: PipelineState.RateLimited,
                waitUntilTime: waitUntilTimeMs,
                globalRetryCount: state.GlobalRetryCount + 1
            );
        }

        private long CalculateWaitUntilTimeMs(int? retryAfterSeconds, long currentTime)
        {
            int seconds = retryAfterSeconds.HasValue
                ? Math.Max(retryAfterSeconds.Value, 0)
                : _config.RateLimitConfig.MaxRetryInterval;
            int clampedSeconds = Math.Min(seconds, _config.RateLimitConfig.MaxRetryInterval);
            return currentTime + (clampedSeconds * 1000L);
        }

        private RetryState HandleRetryableError(RetryState state, ResponseInfo response, long currentTime)
        {
            BatchMetadata existing;
            state.BatchMetadata.TryGetValue(response.BatchFile, out existing);

            int newFailureCount = (existing?.FailureCount ?? 0) + 1;
            long firstFailureTime = existing?.FirstFailureTime ?? currentTime;
            long nextRetryTime = currentTime + CalculateBackoffMs(newFailureCount);

            var newMetadata = new BatchMetadata(
                failureCount: newFailureCount,
                nextRetryTime: nextRetryTime,
                firstFailureTime: firstFailureTime
            );

            return state.SetBatchMetadata(response.BatchFile, newMetadata);
        }

        private long CalculateBackoffMs(int failureCount)
        {
            double baseMs = _config.BackoffConfig.BaseBackoffInterval * 1000;
            long maxMs = _config.BackoffConfig.MaxBackoffInterval * 1000L;

            double exponentialBackoff = baseMs * Math.Pow(2.0, failureCount - 1);
            double cappedBackoff = Math.Min(exponentialBackoff, maxMs);

            double jitterAmount = cappedBackoff * (_config.BackoffConfig.JitterPercent / 100.0);
            double jitter = _random.NextDouble() * jitterAmount;

            return (long)Math.Min(cappedBackoff + jitter, maxMs);
        }

        private RetryBehavior ResolveStatusCodeBehavior(int code)
        {
            RetryBehavior overrideBehavior;
            if (_config.BackoffConfig.StatusCodeOverrides.TryGetValue(code, out overrideBehavior))
                return overrideBehavior;

            if (code >= 400 && code <= 499)
                return _config.BackoffConfig.Default4xxBehavior;
            if (code >= 500 && code <= 599)
                return _config.BackoffConfig.Default5xxBehavior;
            return _config.BackoffConfig.UnknownCodeBehavior;
        }

        private static Dictionary<string, BatchMetadata> RemoveFromMetadata(RetryState state, string batchFile)
        {
            if (!state.BatchMetadata.ContainsKey(batchFile))
                return state.BatchMetadata;

            var newMetadata = new Dictionary<string, BatchMetadata>(state.BatchMetadata);
            newMetadata.Remove(batchFile);
            return newMetadata;
        }
    }
}
