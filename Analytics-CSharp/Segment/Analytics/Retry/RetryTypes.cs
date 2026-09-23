namespace Segment.Analytics.Retry
{
    internal enum PipelineState
    {
        Ready,
        RateLimited
    }

    public enum RetryBehavior
    {
        Retry,
        Drop
    }

    internal enum DropReason
    {
        MaxRetriesExceeded,
        MaxDurationExceeded,
        NonRetryableError
    }

    internal abstract class UploadDecision
    {
        public static readonly UploadDecision Proceed = new ProceedDecision();
        public static readonly UploadDecision SkipThisBatch = new SkipThisBatchDecision();
        public static readonly UploadDecision SkipAllBatches = new SkipAllBatchesDecision();

        public static UploadDecision DropBatch(DropReason reason) => new DropBatchDecision(reason);

        private UploadDecision() { }

        internal sealed class ProceedDecision : UploadDecision { }
        internal sealed class SkipThisBatchDecision : UploadDecision { }
        internal sealed class SkipAllBatchesDecision : UploadDecision { }

        internal sealed class DropBatchDecision : UploadDecision
        {
            public DropReason Reason { get; }
            public DropBatchDecision(DropReason reason) { Reason = reason; }
        }
    }

    internal class ResponseInfo
    {
        public int StatusCode { get; }
        public int? RetryAfterSeconds { get; }
        public string BatchFile { get; }
        public long CurrentTime { get; }

        public ResponseInfo(int statusCode, int? retryAfterSeconds, string batchFile, long currentTime)
        {
            StatusCode = statusCode;
            RetryAfterSeconds = retryAfterSeconds;
            BatchFile = batchFile;
            CurrentTime = currentTime;
        }
    }
}
