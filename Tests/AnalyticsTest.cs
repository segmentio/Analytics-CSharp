using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Moq;
using Segment.Analytics;
using Segment.Analytics.Plugins;
using Segment.Analytics.Utilities;
using Segment.Serialization;
using Tests.Utils;
using Xunit;
using Configuration = Segment.Analytics.Configuration;

namespace Tests
{
    public class AnalyticsTest
    {
        private readonly Analytics _analytics;

        private Mock<IStorage> _storage;

        private Settings? _settings;

        public AnalyticsTest()
        {
            _settings = JsonUtility.FromJson<Settings?>(
                "{\"integrations\":{\"Segment.io\":{\"apiKey\":\"1vNgUqwJeCHmqgI9S1sOm9UHCyfYqbaQ\"}},\"plan\":{},\"edgeFunction\":{}}");

            var mockHttpClient = new Mock<HTTPClient>(null, null, null);
            mockHttpClient
                .Setup(httpClient => httpClient.Settings())
                .ReturnsAsync(_settings);

            _storage = new Mock<IStorage>();
            _storage.Setup(Storage => Storage.RemoveFile("")).Returns(true);
            _storage.Setup(Storage => Storage.Read(StorageConstants.Events)).Returns("test,foo");

            var config = new Configuration(
                writeKey: "123",
                storageProvider: new MockStorageProvider(_storage),
                autoAddSegmentDestination: false,
                useSynchronizeDispatcher: true,
                httpClientProvider: new MockHttpClientProvider(mockHttpClient)
            );
            _analytics = new Analytics(config);
        }

        [Fact]
        public void TestProcess()
        {
            var actual = new TrackEvent("test", new JsonObject());
            _analytics.Process(actual);

            Assert.NotNull(actual.MessageId);
            Assert.NotNull(actual.Context);
            Assert.NotNull(actual.Timestamp);

            Assert.True(actual.UserId != null || actual.AnonymousId != null);
            Assert.NotNull(actual.Integrations);
        }

        [Fact]
        public void TestAnonymousId()
        {
            string id = _analytics.AnonymousId();
            Assert.NotNull(id);
        }

        [Fact]
        public void TestUserId()
        {
            string expected = "test";
            _analytics.Identify(expected);

            string actual = _analytics.UserId();
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TestTraits()
        {
            var expected = new JsonObject
            {
                ["foo"] = "bar"
            };
            _analytics.Identify("test", expected);

            JsonObject actual = _analytics.Traits();
            Assert.Equal(expected.ToString(), actual.ToString());
        }

        [Fact]
        public void TestTraitsT()
        {
            var expected = new FooBar();
            _analytics.Identify("test", expected);

            FooBar actual = _analytics.Traits<FooBar>();
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TestFlush()
        {
            var plugin = new Mock<DestinationPlugin>
            {
                // need this setting to prevent faking internal methods
                CallBase = true
            };
            plugin.Setup(o => o.Flush()).Verifiable();

            _analytics.Add(plugin.Object);
            _analytics.Flush();

            plugin.Verify(o => o.Flush(), Times.Exactly(1));
        }

        [Fact]
        public void TestReset()
        {
            var plugin = new Mock<DestinationPlugin>
            {
                // need this setting to prevent faking internal methods
                CallBase = true
            };
            plugin.Setup(o => o.Reset()).Verifiable();
            plugin.Setup(o => o.Key).Returns("mock");

            string anonIdOld = _analytics.AnonymousId();
            _analytics.Add(plugin.Object);
            _analytics.Identify("test");
            _analytics.Reset();

            string actual = _analytics.UserId();
            string anonIdNew = _analytics.AnonymousId();
            plugin.Verify(o => o.Reset(), Times.Exactly(1));
            Assert.Null(actual);
            Assert.NotEqual(anonIdOld, anonIdNew);
        }

        [Fact]
        public void TestVersion()
        {
            Assert.Equal(Version.SegmentVersion, _analytics.Version);
        }

        [Fact]
        public void TestSettings()
        {
            Settings? actual = _analytics.Settings();
            Assert.Equal(_settings, actual);
        }

        [Fact]
        public async Task TestSettingsAsync()
        {
            Settings? actual = await _analytics.SettingsAsync();
            Assert.Equal(_settings, actual);
        }

        [Fact]
        public void TestDisable()
        {
            _analytics.Enable = false;
            var actual = new TrackEvent("test", new JsonObject());
            _analytics.Process(actual);
            Assert.Null(actual.MessageId);
            Assert.Null(actual.Context);
            Assert.Null(actual.Timestamp);
            Assert.Null(actual.UserId);
            Assert.Null(actual.AnonymousId);
            Assert.Null(actual.Integrations);

            _analytics.Enable = true;
            actual = new TrackEvent("test", new JsonObject());
            _analytics.Process(actual);
            Assert.NotNull(actual.MessageId);
            Assert.NotNull(actual.Context);
            Assert.NotNull(actual.Timestamp);
            Assert.True(actual.UserId != null || actual.AnonymousId != null);
            Assert.NotNull(actual.Integrations);
        }

        [Fact]
        public void TestPurge()
        {
            _analytics.PurgeStorage();
            _storage.Verify(o => o.RemoveFile("test"), Times.Exactly(1));
            _storage.Verify(o => o.RemoveFile("foo"), Times.Exactly(1));

            _analytics.PurgeStorage("bar");
            _storage.Verify(o => o.RemoveFile("bar"), Times.Exactly(1));
        }
    }
}
