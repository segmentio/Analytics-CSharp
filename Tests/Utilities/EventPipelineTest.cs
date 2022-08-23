using System;
using System.IO;
using System.Threading.Tasks;
using Moq;
using Segment.Analytics;
using Segment.Analytics.Utilities;
using Segment.Concurrent;
using Segment.Serialization;
using Segment.Sovran;
using Xunit;

namespace Tests.Utilities
{
    public class EventPipelineTest : IDisposable
    {
        private EventPipeline _eventPipeline;

        private Analytics _analytics;

        private Mock<Storage> _storage;

        private Mock<HTTPClient> _mockHttpClient;

        private string file;

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
                .Setup(httpclient => httpclient.Upload(It.IsAny<string>()))
                .ReturnsAsync(true);

            var store = new Store(true);
            var dispatcher = new SynchronizeDispatcher();
            _storage = new Mock<Storage>(store, "123", "tests", dispatcher);
            _analytics = new Analytics(config,
                store: store,
                analyticsDispatcher: dispatcher,
                fileIODispatcher: dispatcher,
                networkIODispatcher: dispatcher,
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
            _storage
                .Setup(o => o.Read(Storage.Constants.Events))
                .Returns(file);
            // since we can't mock static api, we have to actually create the file
            // the the flushing flow continues
            File.Create(file);
        }

        public void Dispose()
        {
            // clean up
            File.Delete(file);
        }

        [Fact]
        public void TestPut()
        {
            _eventPipeline.Start();
            _eventPipeline.Put("test");

            _storage.Verify(o => o.Write(Storage.Constants.Events, It.IsAny<string>()), Times.Exactly(1));
        }
        
        [Fact]
        public async Task TestFlush()
        {   
            _eventPipeline.Start();
            _eventPipeline.Put("test");
            _eventPipeline.Flush();

            await Task.Delay(1000);
            
            _storage.Verify(o => o.Rollover(), Times.Exactly(1));
            _storage.Verify(o => o.Read(Storage.Constants.Events), Times.Exactly(1));
            _mockHttpClient.Verify(o => o.Upload(file), Times.Exactly(1));
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
            _storage.Verify(o => o.Read(Storage.Constants.Events), Times.Exactly(1));
            _mockHttpClient.Verify(o => o.Upload(file), Times.Exactly(1));
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
            _storage.Verify(o => o.Read(Storage.Constants.Events), Times.Exactly(2));
            _mockHttpClient.Verify(o => o.Upload(file), Times.Exactly(2));
            _storage.Verify(o => o.RemoveFile(file), Times.Exactly(2));
        }
        
        [Fact]
        public async Task TestFlushInterruptedWhenNoFileExist()
        {
            // make sure the file does not exist
            File.Delete(file);
            
            _eventPipeline.Start();
            _eventPipeline.Flush();

            await Task.Delay(1000);
            
            _storage.Verify(o => o.Rollover(), Times.Exactly(1));
            _storage.Verify(o => o.Read(Storage.Constants.Events), Times.Exactly(1));
            _mockHttpClient.Verify(o => o.Upload(file), Times.Exactly(0));
            _storage.Verify(o => o.RemoveFile(file), Times.Exactly(0));
        }
    }
}