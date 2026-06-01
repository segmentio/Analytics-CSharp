using System.Collections.Generic;
using Moq;
using Segment.Analytics.Retry;
using Segment.Analytics.Utilities;
using Xunit;

namespace Tests.Retry
{
    public class RetryStateStorageTest
    {
        private readonly Mock<IStorage> _storage;
        private string _savedValue;

        public RetryStateStorageTest()
        {
            _storage = new Mock<IStorage>();
            _storage
                .Setup(s => s.WritePrefs(StorageConstants.RetryState, It.IsAny<string>()))
                .Callback<StorageConstants, string>((_, value) => _savedValue = value);
            _storage
                .Setup(s => s.Read(StorageConstants.RetryState))
                .Returns(() => _savedValue);
        }

        [Fact]
        public void RoundTrip_DefaultState()
        {
            var state = new RetryState();
            RetryStateStorage.SaveRetryState(_storage.Object, state);
            RetryState loaded = RetryStateStorage.LoadRetryState(_storage.Object);

            Assert.Equal(PipelineState.Ready, loaded.PipelineState);
            Assert.Null(loaded.WaitUntilTime);
            Assert.Equal(0, loaded.GlobalRetryCount);
            Assert.Empty(loaded.BatchMetadata);
        }

        [Fact]
        public void RoundTrip_RateLimitedState()
        {
            var state = new RetryState(
                pipelineState: PipelineState.RateLimited,
                waitUntilTime: 123456789L,
                globalRetryCount: 3);

            RetryStateStorage.SaveRetryState(_storage.Object, state);
            RetryState loaded = RetryStateStorage.LoadRetryState(_storage.Object);

            Assert.Equal(PipelineState.RateLimited, loaded.PipelineState);
            Assert.Equal(123456789L, loaded.WaitUntilTime);
            Assert.Equal(3, loaded.GlobalRetryCount);
        }

        [Fact]
        public void RoundTrip_WithBatchMetadata()
        {
            var metadata = new Dictionary<string, BatchMetadata>
            {
                { "file1.json", new BatchMetadata(failureCount: 2, nextRetryTime: 5000, firstFailureTime: 1000) },
                { "file2.json", new BatchMetadata(failureCount: 1, nextRetryTime: 3000, firstFailureTime: 2000) }
            };
            var state = new RetryState(batchMetadata: metadata);

            RetryStateStorage.SaveRetryState(_storage.Object, state);
            RetryState loaded = RetryStateStorage.LoadRetryState(_storage.Object);

            Assert.Equal(2, loaded.BatchMetadata.Count);
            Assert.Equal(2, loaded.BatchMetadata["file1.json"].FailureCount);
            Assert.Equal(5000L, loaded.BatchMetadata["file1.json"].NextRetryTime);
            Assert.Equal(1000L, loaded.BatchMetadata["file1.json"].FirstFailureTime);
            Assert.Equal(1, loaded.BatchMetadata["file2.json"].FailureCount);
        }

        [Fact]
        public void RoundTrip_WindowsPathKeys()
        {
            // Batch keys are absolute file paths; on Windows they contain backslashes.
            // The serializer escapes them, so the parser must unescape them back to the
            // exact live key — otherwise per-batch retry metadata is orphaned after a restart.
            const string winPath = @"C:\Users\x\AppData\Local\segment\segment.events.123.tmp";
            var metadata = new Dictionary<string, BatchMetadata>
            {
                { winPath, new BatchMetadata(failureCount: 4, nextRetryTime: 9000, firstFailureTime: 1000) }
            };
            var state = new RetryState(batchMetadata: metadata);

            RetryStateStorage.SaveRetryState(_storage.Object, state);
            RetryState loaded = RetryStateStorage.LoadRetryState(_storage.Object);

            Assert.True(loaded.BatchMetadata.ContainsKey(winPath));
            Assert.Equal(4, loaded.BatchMetadata[winPath].FailureCount);
            Assert.Equal(9000L, loaded.BatchMetadata[winPath].NextRetryTime);
            Assert.Equal(1000L, loaded.BatchMetadata[winPath].FirstFailureTime);
        }

        [Fact]
        public void RoundTrip_KeyWithEscapedQuoteAndTrailingBatch()
        {
            // A key containing a quote must not terminate the scan early and corrupt
            // parsing of subsequent batches.
            const string trickyKey = "file\"with\"quotes.tmp";
            const string plainKey = "file2.tmp";
            var metadata = new Dictionary<string, BatchMetadata>
            {
                { trickyKey, new BatchMetadata(failureCount: 1, nextRetryTime: 1111, firstFailureTime: 2222) },
                { plainKey, new BatchMetadata(failureCount: 7, nextRetryTime: 3333, firstFailureTime: 4444) }
            };
            var state = new RetryState(batchMetadata: metadata);

            RetryStateStorage.SaveRetryState(_storage.Object, state);
            RetryState loaded = RetryStateStorage.LoadRetryState(_storage.Object);

            Assert.Equal(2, loaded.BatchMetadata.Count);
            Assert.True(loaded.BatchMetadata.ContainsKey(trickyKey));
            Assert.Equal(1, loaded.BatchMetadata[trickyKey].FailureCount);
            Assert.True(loaded.BatchMetadata.ContainsKey(plainKey));
            Assert.Equal(7, loaded.BatchMetadata[plainKey].FailureCount);
            Assert.Equal(3333L, loaded.BatchMetadata[plainKey].NextRetryTime);
        }

        [Fact]
        public void LoadRetryState_NullStorage_ReturnsDefault()
        {
            _storage.Setup(s => s.Read(StorageConstants.RetryState)).Returns((string)null);

            RetryState loaded = RetryStateStorage.LoadRetryState(_storage.Object);

            Assert.Equal(PipelineState.Ready, loaded.PipelineState);
            Assert.Equal(0, loaded.GlobalRetryCount);
        }

        [Fact]
        public void LoadRetryState_EmptyString_ReturnsDefault()
        {
            _storage.Setup(s => s.Read(StorageConstants.RetryState)).Returns("");

            RetryState loaded = RetryStateStorage.LoadRetryState(_storage.Object);

            Assert.Equal(PipelineState.Ready, loaded.PipelineState);
        }

        [Fact]
        public void LoadRetryState_CorruptJson_ReturnsDefault()
        {
            _storage.Setup(s => s.Read(StorageConstants.RetryState)).Returns("not valid json{{{");

            RetryState loaded = RetryStateStorage.LoadRetryState(_storage.Object);

            Assert.Equal(PipelineState.Ready, loaded.PipelineState);
        }

        [Fact]
        public void ClearRetryState_RemovesKey()
        {
            RetryStateStorage.ClearRetryState(_storage.Object);

            _storage.Verify(s => s.Remove(StorageConstants.RetryState), Times.Once);
        }
    }
}
