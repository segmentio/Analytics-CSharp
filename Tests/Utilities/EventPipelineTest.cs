using System;
using System.Threading.Tasks;
using Moq;
using Segment.Analytics;
using Segment.Analytics.Plugins;
using Segment.Analytics.Policies;
using Segment.Analytics.Utilities;
using Segment.Serialization;
using Tests.Utils;
using Xunit;

namespace Tests.Utilities
{
    public class EventPipelineTest
    {
        private EventPipeline _eventPipeline;

        private readonly Analytics _analytics;

        private readonly Mock<IStorage> _storage;

        private readonly Mock<HTTPClient> _mockHttpClient;

        private readonly string _file;

        private readonly byte[] _bytes;

        public EventPipelineTest()
        {
            Settings? settings = JsonUtility.FromJson<Settings?>(
                "{\"integrations\":{\"Segment.io\":{\"apiKey\":\"1vNgUqwJeCHmqgI9S1sOm9UHCyfYqbaQ\"}},\"plan\":{},\"edgeFunction\":{}}");

            _mockHttpClient = new Mock<HTTPClient>(null, null, null);
            _mockHttpClient
                .Setup(httpClient => httpClient.Settings())
                .ReturnsAsync(settings);
            _mockHttpClient
                .Setup(httpclient => httpclient.Upload(It.IsAny<byte[]>()))
                .ReturnsAsync(true);

            _storage = new Mock<IStorage>();

            var config = new Configuration(
                writeKey: "123",
                autoAddSegmentDestination: false,
                userSynchronizeDispatcher: true,
                flushInterval: 0,
                flushAt: 2,
                httpClientProvider: new MockHttpClientProvider(_mockHttpClient),
                storageProvider: new MockStorageProvider(_storage)
            );
            _analytics = new Analytics(config);
            _eventPipeline = new EventPipeline(
                _analytics,
                logTag: "key",
                apiKey: _analytics.Configuration.WriteKey,
                flushPolicies: _analytics.Configuration.FlushPolicies,
                apiHost: _analytics.Configuration.ApiHost
            );

            _file = Guid.NewGuid().ToString();
            _bytes = _file.GetBytes();
            _storage
                .Setup(o => o.Read(StorageConstants.Events))
                .Returns(_file);
            _storage
                .Setup(o => o.ReadAsBytes(It.IsAny<string>()))
                .Returns(_bytes);
        }

        [Fact]
        public async Task TestPut()
        {
            _eventPipeline.Start();
            _eventPipeline.Put(new ScreenEvent("test"));

            await Task.Delay(1000);

            _storage.Verify(o => o.Write(StorageConstants.Events, It.IsAny<string>()), Times.Exactly(1));
        }

        [Fact]
        public async Task TestFlush()
        {
            _eventPipeline.Start();
            _eventPipeline.Put(new ScreenEvent("test"));
            _eventPipeline.Flush();

            await Task.Delay(1000);

            _storage.Verify(o => o.Rollover(), Times.Exactly(1));
            _storage.Verify(o => o.Read(StorageConstants.Events), Times.Exactly(1));
            _mockHttpClient.Verify(o => o.Upload(_bytes), Times.Exactly(1));
            _storage.Verify(o => o.RemoveFile(_file), Times.Exactly(1));
        }

        [Fact]
        public void TestStart()
        {
            _eventPipeline.Start();
            Assert.True(_eventPipeline.Running);
        }

        [Fact]
        public async void TestStop()
        {
            _eventPipeline.Stop();
            Assert.False(_eventPipeline.Running);

            // make sure writeChannel is stopped
            _eventPipeline.Put(new ScreenEvent("test"));
            await Task.Delay(1000);
            _storage.Verify(o => o.Write(StorageConstants.Events, It.IsAny<string>()), Times.Never);

            // make sure uploadChannel is stopped
            _eventPipeline.Flush();
            await Task.Delay(1000);
            _storage.Verify(o => o.Rollover(), Times.Never);
            _storage.Verify(o => o.Read(StorageConstants.Events), Times.Never);
            _mockHttpClient.Verify(o => o.Upload(_bytes), Times.Never);
            _storage.Verify(o => o.RemoveFile(_file), Times.Never);
        }

        [Fact]
        public async Task TestFlushCausedByOverflow()
        {
            _eventPipeline.Start();
            _eventPipeline.Put(new ScreenEvent("event 1"));
            _eventPipeline.Put(new ScreenEvent("event 2"));

            await Task.Delay(1000);

            _storage.Verify(o => o.Rollover(), Times.Exactly(1));
            _storage.Verify(o => o.Read(StorageConstants.Events), Times.Exactly(1));
            _mockHttpClient.Verify(o => o.Upload(_bytes), Times.Exactly(1));
            _storage.Verify(o => o.RemoveFile(_file), Times.Exactly(1));
        }

        [Fact]
        public async Task TestPeriodicalFlush()
        {
            foreach (IFlushPolicy policy in _analytics.Configuration.FlushPolicies)
            {
                if (policy is FrequencyFlushPolicy frequencyFlushPolicy)
                {
                    frequencyFlushPolicy.FlushIntervalInMills = 1000L;
                }
            }

            // since we set autoAddSegmentDestination = false, we need to manually add it to analytics.
            // we need a mocked SegmentDestination so we can redirect Flush call to this _eventPipeline.
            var segmentDestination = new Mock<SegmentDestination>();
            segmentDestination.Setup(o => o.Flush()).Callback(() => _eventPipeline.Flush());
            segmentDestination.Setup(o => o.Analytics).Returns(_analytics);
            _analytics.Add(segmentDestination.Object);

            _eventPipeline = new EventPipeline(
                _analytics,
                logTag: "key",
                apiKey: _analytics.Configuration.WriteKey,
                flushPolicies: _analytics.Configuration.FlushPolicies,
                apiHost: _analytics.Configuration.ApiHost
            );
            _eventPipeline.Start();
            _eventPipeline.Put(new ScreenEvent("test"));

            await Task.Delay(1500);

            _storage.Verify(o => o.Rollover(), Times.Exactly(2));
            _storage.Verify(o => o.Read(StorageConstants.Events), Times.Exactly(2));
            _mockHttpClient.Verify(o => o.Upload(_bytes), Times.Exactly(2));
            _storage.Verify(o => o.RemoveFile(_file), Times.Exactly(2));
        }

        [Fact]
        public async Task TestFlushInterruptedWhenNoFileExist()
        {
            // make sure the file does not exist
            _storage
                .Setup(o => o.ReadAsBytes(It.IsAny<string>()))
                .Returns((byte[])null);

            _eventPipeline.Start();
            _eventPipeline.Flush();

            await Task.Delay(1000);

            _storage.Verify(o => o.Rollover(), Times.Exactly(1));
            _storage.Verify(o => o.Read(StorageConstants.Events), Times.Exactly(1));
            _mockHttpClient.Verify(o => o.Upload(_bytes), Times.Exactly(0));
            _storage.Verify(o => o.RemoveFile(_file), Times.Exactly(0));
        }
    }
}
