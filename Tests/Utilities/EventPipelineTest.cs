using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Segment.Analytics;
using Segment.Analytics.Plugins;
using Segment.Analytics.Policies;
using Segment.Analytics.Utilities;
using Segment.Serialization;
using Tests.Utils;
using Xunit;
using System.Linq;

namespace Tests.Utilities
{
    public class EventPipelineTest
    {
        private readonly Analytics _analytics;

        private readonly Mock<IStorage> _storage;

        private readonly Mock<HTTPClient> _mockHttpClient;

        private readonly string _file;

        private readonly byte[] _bytes;

        public static IEnumerable<object[]> GetPipelineProvider()
        {
            yield return new object[] { new EventPipelineProvider() };
            yield return new object[] { new SyncEventPipelineProvider() };
        }

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
                useSynchronizeDispatcher: true,
                flushInterval: 0,
                flushAt: 2,
                httpClientProvider: new MockHttpClientProvider(_mockHttpClient),
                storageProvider: new MockStorageProvider(_storage)
            );
            _analytics = new Analytics(config);
            _file = Guid.NewGuid().ToString();
            _bytes = _file.GetBytes();
            _storage
                .Setup(o => o.Read(StorageConstants.Events))
                .Returns(_file);
            _storage
                .Setup(o => o.ReadAsBytes(It.IsAny<string>()))
                .Returns(_bytes);
        }

        [Theory]
        [MemberData(nameof(GetPipelineProvider))]
        public async Task TestPut(IEventPipelineProvider provider)
        {
            IEventPipeline eventPipeline = provider.Create(_analytics, "key");
            eventPipeline.Start();
            eventPipeline.Put(new ScreenEvent("test"));

            await Task.Delay(1000);

            _storage.Verify(o => o.Write(StorageConstants.Events, It.IsAny<string>()), Times.Exactly(1));
        }

        [Theory]
        [MemberData(nameof(GetPipelineProvider))]
        public async Task TestFlush(IEventPipelineProvider provider)
        {
            IEventPipeline eventPipeline = provider.Create(_analytics, "key");
            eventPipeline.Start();
            eventPipeline.Put(new ScreenEvent("test"));
            eventPipeline.Flush();

            await Task.Delay(1000);

            _storage.Verify(o => o.Rollover(), Times.Exactly(1));
            _storage.Verify(o => o.Read(StorageConstants.Events), Times.Exactly(1));
            _mockHttpClient.Verify(o => o.Upload(_bytes), Times.Exactly(1));
            _storage.Verify(o => o.RemoveFile(_file), Times.Exactly(1));
        }

        [Theory]
        [MemberData(nameof(GetPipelineProvider))]
        public void TestStart(IEventPipelineProvider provider)
        {
            IEventPipeline eventPipeline = provider.Create(_analytics, "key");
            eventPipeline.Start();
            Assert.True(eventPipeline.Running);
        }

        [Theory]
        [MemberData(nameof(GetPipelineProvider))]
        public async void TestStop(IEventPipelineProvider provider)
        {
            IEventPipeline eventPipeline = provider.Create(_analytics, "key");
            eventPipeline.Start();
            Assert.True(eventPipeline.Running);
            eventPipeline.Stop();
            Assert.False(eventPipeline.Running);

            // make sure writeChannel is stopped
            eventPipeline.Put(new ScreenEvent("test"));
            await Task.Delay(1000);
            _storage.Verify(o => o.Write(StorageConstants.Events, It.IsAny<string>()), Times.Never);

            // make sure uploadChannel is stopped
            eventPipeline.Flush();
            await Task.Delay(1000);
            _storage.Verify(o => o.Rollover(), Times.Never);
            _storage.Verify(o => o.Read(StorageConstants.Events), Times.Never);
            _mockHttpClient.Verify(o => o.Upload(_bytes), Times.Never);
            _storage.Verify(o => o.RemoveFile(_file), Times.Never);
        }

        [Theory]
        [MemberData(nameof(GetPipelineProvider))]
        public async Task TestFlushCausedByOverflow(IEventPipelineProvider provider)
        {
            IEventPipeline eventPipeline = provider.Create(_analytics, "key");
            eventPipeline.Start();
            eventPipeline.Put(new ScreenEvent("event 1"));
            eventPipeline.Put(new ScreenEvent("event 2"));

            await Task.Delay(1000);

            _storage.Verify(o => o.Rollover(), Times.Exactly(1));
            _storage.Verify(o => o.Read(StorageConstants.Events), Times.Exactly(1));
            _mockHttpClient.Verify(o => o.Upload(_bytes), Times.Exactly(1));
            _storage.Verify(o => o.RemoveFile(_file), Times.Exactly(1));
        }

        [Theory]
        [MemberData(nameof(GetPipelineProvider))]
        public async Task TestPeriodicalFlush(IEventPipelineProvider provider)
        {
            IEventPipeline eventPipeline = provider.Create(_analytics, "key");
            foreach (IFlushPolicy policy in _analytics.Configuration.FlushPolicies)
            {
                if (policy is FrequencyFlushPolicy)
                {
                    _analytics.RemoveFlushPolicy(policy);
                }
            }
            _analytics.AddFlushPolicy(new FrequencyFlushPolicy(1000L));

            // since we set autoAddSegmentDestination = false, we need to manually add it to analytics.
            // we need a mocked SegmentDestination so we can redirect Flush call to this eventPipeline.
            var segmentDestination = new Mock<SegmentDestination>();
            segmentDestination.Setup(o => o.Flush()).Callback(() => eventPipeline.Flush());
            segmentDestination.Setup(o => o.Analytics).Returns(_analytics);
            _analytics.Add(segmentDestination.Object);

            eventPipeline = new EventPipeline(
                _analytics,
                logTag: "key",
                apiKey: _analytics.Configuration.WriteKey,
                flushPolicies: _analytics.Configuration.FlushPolicies,
                apiHost: _analytics.Configuration.ApiHost
            );
            eventPipeline.Start();
            eventPipeline.Put(new ScreenEvent("test"));

            await Task.Delay(2050);

            _storage.Verify(o => o.Rollover(), Times.Exactly(2));
            _storage.Verify(o => o.Read(StorageConstants.Events), Times.Exactly(2));
            _mockHttpClient.Verify(o => o.Upload(_bytes), Times.Exactly(2));
            _storage.Verify(o => o.RemoveFile(_file), Times.Exactly(2));
        }

        [Theory]
        [MemberData(nameof(GetPipelineProvider))]
        public async Task TestFlushInterruptedWhenNoFileExist(IEventPipelineProvider provider)
        {
            IEventPipeline eventPipeline = provider.Create(_analytics, "key");
            // make sure the file does not exist
            _storage
                .Setup(o => o.ReadAsBytes(It.IsAny<string>()))
                .Returns((byte[])null);

            eventPipeline.Start();
            eventPipeline.Flush();

            await Task.Delay(1000);

            _storage.Verify(o => o.Rollover(), Times.Exactly(1));
            _storage.Verify(o => o.Read(StorageConstants.Events), Times.Exactly(1));
            _mockHttpClient.Verify(o => o.Upload(_bytes), Times.Exactly(0));
            _storage.Verify(o => o.RemoveFile(_file), Times.Exactly(0));
        }

        [Theory]
        [MemberData(nameof(GetPipelineProvider))]
        public void TestConfigWithEventPipelineProviders(IEventPipelineProvider provider)
        {
            // Just validate that the provider is used in the configuration
            var config = new Configuration(
                writeKey: "123",
                autoAddSegmentDestination: false,
                useSynchronizeDispatcher: true,
                flushInterval: 0,
                flushAt: 2,
                httpClientProvider: new MockHttpClientProvider(_mockHttpClient),
                storageProvider: new MockStorageProvider(_storage),
                eventPipelineProvider: provider
            );
            var analytics = new Analytics(config);
            analytics.Track("test");
        }

        [Fact]
        public void TestSyncEventPipelineProviderWaits()
        {
            const int iterations = 100;
            const int newAnalyticsEvery = 10;
            const int eventCount = 10;

            int totalTracks = 0;
            int totalUploads = 0;

            _mockHttpClient
            .Setup(client => client.Upload(It.IsAny<byte[]>()))
            .Callback<byte[]>(bytes =>
            {
                string content = System.Text.Encoding.UTF8.GetString(bytes);
                int count = content.Split(new string[] { "test" }, StringSplitOptions.None).Length - 1;
                totalUploads += count;
            })
            .ReturnsAsync(true);

            var config = new Configuration(
            writeKey: "123",
            useSynchronizeDispatcher: true,
            flushInterval: 100000,
            flushAt: eventCount * 2,
            httpClientProvider: new MockHttpClientProvider(_mockHttpClient),
            storageProvider: new InMemoryStorageProvider(),
            eventPipelineProvider: new SyncEventPipelineProvider()
            );

            var analytics = new Analytics(config);
            for (int j = 0; j < iterations; j++) 
            {
                if (j % newAnalyticsEvery == 0)
                {
                    analytics = new Analytics(config);
                }
                _mockHttpClient.Invocations.Clear();
                for (int i = 0; i < eventCount; i++)
                {
                    analytics.Track($"test {i}");
                    totalTracks++;
                }
                analytics.Flush();

#pragma warning disable CS4014 // Silly compiler, this isn't an invocation so it doesn't need to be awaited
                _mockHttpClient.Verify(client => client.Upload(It.IsAny<byte[]>()), Times.AtLeastOnce, $"Iteration {j} of {eventCount}");
#pragma warning restore CS4014 
                IInvocation lastUploadInvocation = _mockHttpClient.Invocations.Last(invocation => invocation.Method.Name == "Upload");
                int testsUploaded = System.Text.Encoding.UTF8
                    .GetString((byte[])lastUploadInvocation.Arguments[0])
                    .Split(new string[] { "test" }, StringSplitOptions.None).Length - 1;
                Assert.Equal(eventCount, testsUploaded);
            }
            Assert.Equal(totalTracks, totalUploads);
        }

        [Fact]
        public void TestRepeatedFlushesDontHang()
        {
            var config = new Configuration(
                writeKey: "123",
                useSynchronizeDispatcher: true,
                flushInterval: 0,
                flushAt: 1,
                httpClientProvider: new MockHttpClientProvider(_mockHttpClient),
                storageProvider: new MockStorageProvider(_storage),
                eventPipelineProvider: new SyncEventPipelineProvider(5000)
            );
            var analytics = new Analytics(config);
            analytics.Track("test");
            DateTime startTime = DateTime.Now;
            analytics.Flush();
            analytics.Flush();
            analytics.Flush();
            analytics.Flush();
            analytics.Flush();
            Assert.True(DateTime.Now - startTime < TimeSpan.FromMilliseconds(100));
        }

        [Fact]
        public void TestConfigWithCustomEventPipelineProvider()
        {
            // Just validate that the provider is used in the configuration
            var config = new Configuration(
                writeKey: "123",
                useSynchronizeDispatcher: true,
                flushInterval: 0,
                flushAt: 1,
                httpClientProvider: new MockHttpClientProvider(_mockHttpClient),
                storageProvider: new MockStorageProvider(_storage),
                eventPipelineProvider: new CustomEventPipelineProvider()
            );
            Assert.Throws<NotImplementedException>(() => {
                var analytics = new Analytics(config);
                analytics.Track("test");
                analytics.Flush();
            });
        }


        public class CustomEventPipelineProvider : IEventPipelineProvider
        {
            public CustomEventPipelineProvider() {}
            public IEventPipeline Create(Analytics analytics, string key)
            {
                return new CustomEventPipeline(analytics, key);
            }

            private class CustomEventPipeline : IEventPipeline
            {
                public CustomEventPipeline(Analytics analytics, string key) {}
                public bool Running => throw new NotImplementedException();
                public string ApiHost { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
                public void Flush() => throw new NotImplementedException();
                public void Put(RawEvent @event) => throw new NotImplementedException();
                public void Start() => throw new NotImplementedException();
                public void Stop() => throw new NotImplementedException();
            }
        }
    }
}
