using System;
using System.Threading.Tasks;
using Moq;
using Segment.Analytics;
using Segment.Analytics.Utilities;
using Segment.Serialization;
using Xunit;

namespace Tests.Utilities
{
    public class EventPipelineTest
    {
        private EventPipeline _eventPipeline;

        private Analytics _analytics;

        private Mock<IStorage> _storage;

        private Mock<HTTPClient> _mockHttpClient;

        private string file;

        private byte[] _bytes;

        public EventPipelineTest()
        {
            var settings = JsonUtility.FromJson<Settings?>(
                "{\"integrations\":{\"Segment.io\":{\"apiKey\":\"1vNgUqwJeCHmqgI9S1sOm9UHCyfYqbaQ\"}},\"plan\":{},\"edgeFunction\":{}}");

            var config = new Configuration(
                writeKey: "123",
                persistentDataPath: "tests",
                autoAddSegmentDestination: false,
                userSynchronizeDispatcher: true,
                flushInterval: 0,
                flushAt: 2
            );

            _mockHttpClient = new Mock<HTTPClient>(null, null, null);
            _mockHttpClient
                .Setup(httpClient => httpClient.Settings())
                .ReturnsAsync(settings);
            _mockHttpClient
                .Setup(httpclient => httpclient.Upload(It.IsAny<byte[]>()))
                .ReturnsAsync(true);

            _storage = new Mock<IStorage>();
            _analytics = new Analytics(config,
                httpClient: _mockHttpClient.Object,
                storage: _storage.Object);
            _eventPipeline = new EventPipeline(
                _analytics,
                httpClient: _mockHttpClient.Object,
                logTag: "key",
                apiKey: _analytics.configuration.writeKey,
                flushCount: _analytics.configuration.flushAt,
                flushIntervalInMillis: _analytics.configuration.flushInterval * 1000L,
                apiHost: _analytics.configuration.apiHost
            );
            
            file = Guid.NewGuid().ToString();
            _bytes = file.GetBytes();
            _storage
                .Setup(o => o.Read(StorageConstants.Events))
                .Returns(file);
            _storage
                .Setup(o => o.ReadAsBytes(It.IsAny<string>()))
                .Returns(_bytes);
        }

        [Fact]
        public async Task TestPut()
        {
            _eventPipeline.Start();
            _eventPipeline.Put("test");

            await Task.Delay(1000);
            
            _storage.Verify(o => o.Write(StorageConstants.Events, It.IsAny<string>()), Times.Exactly(1));
        }
        
        [Fact]
        public async Task TestFlush()
        {   
            _eventPipeline.Start();
            _eventPipeline.Put("test");
            _eventPipeline.Flush();

            await Task.Delay(1000);
            
            _storage.Verify(o => o.Rollover(), Times.Exactly(1));
            _storage.Verify(o => o.Read(StorageConstants.Events), Times.Exactly(1));
            _mockHttpClient.Verify(o => o.Upload(_bytes), Times.Exactly(1));
            _storage.Verify(o => o.RemoveFile(file), Times.Exactly(1));
        }

        [Fact]
        public void TestStart()
        {
            _eventPipeline.Start();
            Assert.True(_eventPipeline.running);
        }

        [Fact]
        public void TestStop()
        {
            _eventPipeline.Stop();
            Assert.False(_eventPipeline.running);
        }

        [Fact]
        public async Task TestFlushCausedByOverflow()
        {
            _eventPipeline.Start();
            _eventPipeline.Put("event 1");
            _eventPipeline.Put("event 2");
            
            await Task.Delay(1000);
            
            _storage.Verify(o => o.Rollover(), Times.Exactly(1));
            _storage.Verify(o => o.Read(StorageConstants.Events), Times.Exactly(1));
            _mockHttpClient.Verify(o => o.Upload(_bytes), Times.Exactly(1));
            _storage.Verify(o => o.RemoveFile(file), Times.Exactly(1));
        }

        [Fact]
        public async Task TestPeriodicalFlush()
        {
            _eventPipeline = new EventPipeline(
                _analytics,
                httpClient: _mockHttpClient.Object,
                logTag: "key",
                apiKey: _analytics.configuration.writeKey,
                flushCount: _analytics.configuration.flushAt,
                flushIntervalInMillis: 1000L,
                apiHost: _analytics.configuration.apiHost
            );
            _eventPipeline.Start();
            _eventPipeline.Put("test");

            await Task.Delay(1500);
            
            _storage.Verify(o => o.Rollover(), Times.Exactly(2));
            _storage.Verify(o => o.Read(StorageConstants.Events), Times.Exactly(2));
            _mockHttpClient.Verify(o => o.Upload(_bytes), Times.Exactly(2));
            _storage.Verify(o => o.RemoveFile(file), Times.Exactly(2));
        }
        
        [Fact]
        public async Task TestFlushInterruptedWhenNoFileExist()
        {
            // make sure the file does not exist
            _storage
                .Setup(o => o.ReadAsBytes(It.IsAny<string>()))
                .Returns((byte[]) null);
            
            _eventPipeline.Start();
            _eventPipeline.Flush();

            await Task.Delay(1000);
            
            _storage.Verify(o => o.Rollover(), Times.Exactly(1));
            _storage.Verify(o => o.Read(StorageConstants.Events), Times.Exactly(1));
            _mockHttpClient.Verify(o => o.Upload(_bytes), Times.Exactly(0));
            _storage.Verify(o => o.RemoveFile(file), Times.Exactly(0));
        }
    }
}