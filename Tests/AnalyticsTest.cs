using System.Runtime.Serialization;
using System.Threading.Tasks;
using Moq;
using Segment.Analytics;
using Segment.Analytics.Utilities;
using Segment.Serialization;
using Tests.Utils;
using Xunit;
using Configuration = Segment.Analytics.Configuration;

namespace Tests
{
    public class AnalyticsTest
    {
        private Analytics _analytics;

        private Settings? _settings;

        public AnalyticsTest()
        {
            _settings = JsonUtility.FromJson<Settings?>(
                "{\"integrations\":{\"Segment.io\":{\"apiKey\":\"1vNgUqwJeCHmqgI9S1sOm9UHCyfYqbaQ\"}},\"plan\":{},\"edgeFunction\":{}}");

            var config = new Configuration(
                writeKey: "123",
                persistentDataPath: "tests",
                autoAddSegmentDestination: false,
                userSynchronizeDispatcher: true
            );

            var mockHttpClient = new Mock<HTTPClient>(null, null, null);
            mockHttpClient
                .Setup(httpClient => httpClient.Settings())
                .ReturnsAsync(_settings);
            
            _analytics = new Analytics(config, httpClient: mockHttpClient.Object);
        }

        [Fact]
        public void TestProcess()
        { 
            var actual = new TrackEvent("test", new JsonObject());
            _analytics.Process(actual);

            Assert.NotNull(actual.messageId);
            Assert.NotNull(actual.context);
            Assert.NotNull(actual.timestamp);
            
            Assert.True(actual.userId != null || actual.anonymousId != null);
            Assert.NotNull(actual.integrations);
        }

        [Fact]
        public void TestAnonymousId()
        {
            var id = _analytics.AnonymousId;
            Assert.NotNull(id);
        }

        [Fact]
        public void TestUserId()
        {
            var expected = "test";
            _analytics.Identify(expected);

            var actual = _analytics.UserId;
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

            var actual = _analytics.Traits();
            Assert.Equal(expected.ToString(), actual.ToString());
        }
        
        [Fact]
        public void TestTraitsT()
        {
            var expected = new FooBar();
            _analytics.Identify("test", expected);

            var actual = _analytics.Traits<FooBar>();
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
        public async Task TestReset()
        {
            var plugin = new Mock<DestinationPlugin>
            {
                // need this setting to prevent faking internal methods
                CallBase = true
            };
            plugin.Setup(o => o.Reset()).Verifiable();

            _analytics.Add(plugin.Object);
            _analytics.Identify("test");
            _analytics.Reset();

            var actual = _analytics.UserId;
            plugin.Verify(o => o.Reset(), Times.Exactly(1));
            Assert.Null(actual);
        }

        [Fact]
        public void TestVersion()
        {
            Assert.Equal(Version.SegmentVersion, _analytics.version);
        }

        [Fact]
        public void TestSettings()
        {
            var actual = _analytics.Settings();
            Assert.Equal(_settings, actual);
        }
        
        [Fact]
        public async Task TestSettingsAsync()
        {
            var actual = await _analytics.SettingsAsync();
            Assert.Equal(_settings, actual);
        }
    }
}