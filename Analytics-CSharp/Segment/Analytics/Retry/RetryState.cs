using System.Collections.Generic;
using System.Linq;

namespace Segment.Analytics.Retry
{
    internal class BatchMetadata
    {
        public int FailureCount { get; }
        public long? NextRetryTime { get; }
        public long? FirstFailureTime { get; }

        public BatchMetadata(int failureCount = 0, long? nextRetryTime = null, long? firstFailureTime = null)
        {
            FailureCount = failureCount;
            NextRetryTime = nextRetryTime;
            FirstFailureTime = firstFailureTime;
        }

        public bool ShouldRetry(long currentTime)
        {
            if (NextRetryTime == null) return true;
            return currentTime >= NextRetryTime.Value;
        }

        public bool ExceedsMaxDuration(long currentTime, long maxDurationMs)
        {
            if (FirstFailureTime == null) return false;
            return (currentTime - FirstFailureTime.Value) > maxDurationMs;
        }
    }

    internal class RetryState
    {
        public PipelineState PipelineState { get; }
        public long? WaitUntilTime { get; }
        public int GlobalRetryCount { get; }
        public Dictionary<string, BatchMetadata> BatchMetadata { get; }

        private static readonly Dictionary<string, BatchMetadata> s_emptyMetadata =
            new Dictionary<string, BatchMetadata>();

        public RetryState(
            PipelineState pipelineState = PipelineState.Ready,
            long? waitUntilTime = null,
            int globalRetryCount = 0,
            Dictionary<string, BatchMetadata> batchMetadata = null)
        {
            PipelineState = pipelineState;
            WaitUntilTime = waitUntilTime;
            GlobalRetryCount = globalRetryCount;
            BatchMetadata = batchMetadata ?? s_emptyMetadata;
        }

        public bool IsRateLimited(long currentTime)
        {
            return PipelineState == PipelineState.RateLimited
                && WaitUntilTime != null
                && currentTime < WaitUntilTime.Value;
        }

        public RetryState With(
            PipelineState? pipelineState = null,
            long? waitUntilTime = null,
            bool clearWaitUntilTime = false,
            int? globalRetryCount = null,
            Dictionary<string, BatchMetadata> batchMetadata = null)
        {
            return new RetryState(
                pipelineState: pipelineState ?? PipelineState,
                waitUntilTime: clearWaitUntilTime ? null : (waitUntilTime ?? WaitUntilTime),
                globalRetryCount: globalRetryCount ?? GlobalRetryCount,
                batchMetadata: batchMetadata ?? BatchMetadata
            );
        }

        public RetryState RemoveBatch(string batchFile)
        {
            if (!BatchMetadata.ContainsKey(batchFile))
                return this;

            var newMetadata = BatchMetadata.Where(kvp => kvp.Key != batchFile)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            return With(batchMetadata: newMetadata);
        }

        public RetryState SetBatchMetadata(string batchFile, BatchMetadata metadata)
        {
            var newMetadata = new Dictionary<string, BatchMetadata>(BatchMetadata);
            newMetadata[batchFile] = metadata;
            return With(batchMetadata: newMetadata);
        }
    }
}
