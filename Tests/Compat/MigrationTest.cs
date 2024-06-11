using System.Collections.Generic;
using Moq;
using Segment.Analytics;
using Segment.Analytics.Utilities;
using Segment.Analytics.Compat;
using Segment.Serialization;
using Tests.Utils;
using Xunit;

namespace Tests.Compat
{
    public class MigrationTest
    {
        private readonly Analytics _analytics;

        private Settings? _settings;

        private readonly Mock<StubAfterEventPlugin> _plugin;

        public MigrationTest()
        {
            _settings = JsonUtility.FromJson<Settings?>(
                "{\"integrations\":{\"Segment.io\":{\"apiKey\":\"1vNgUqwJeCHmqgI9S1sOm9UHCyfYqbaQ\"}},\"plan\":{},\"edgeFunction\":{}}");

            var mockHttpClient = new Mock<HTTPClient>(null, null, null);
            mockHttpClient
                .Setup(httpClient => httpClient.Settings())
                .ReturnsAsync(_settings);

            _plugin = new Mock<StubAfterEventPlugin>
            {
                CallBase = true
            };

            var config = new Configuration(
                writeKey: "123",
                storageProvider: new DefaultStorageProvider("tests"),
                autoAddSegmentDestination: false,
                useSynchronizeDispatcher: true,
                httpClientProvider: new MockHttpClientProvider(mockHttpClient)
            );
            _analytics = new Analytics(config);
            _analytics.Add(new UserIdPlugin());
        }

        [Fact]
        public void TestCompatTrackAcceptNullDict()
        {
            var actual = new List<TrackEvent>();
            _plugin.Setup(o => o.Track(Capture.In(actual)));
            _analytics.Add(_plugin.Object);
            _analytics.Track("user123", "foo", null);

            Assert.NotEmpty(actual);
            Assert.False(actual[0].Properties.ContainsKey("userId"));
            Assert.Equal("user123", actual[0].UserId);
            Assert.Equal("foo", actual[0].Event);
        }

        [Fact]
        public void TestCompatTrackAcceptDictWithNulls()
        {
            var properties = new Dictionary<string, object>
            {
                ["nullValue"] = null
            };
            var actual = new List<TrackEvent>();
            _plugin.Setup(o => o.Track(Capture.In(actual)));
            _analytics.Add(_plugin.Object);
            _analytics.Track("user123", "foo", properties);

            Assert.NotEmpty(actual);
            Assert.False(actual[0].Properties.ContainsKey("userId"));
            Assert.Equal("user123", actual[0].UserId);
            Assert.Equal("foo", actual[0].Event);
            Assert.True(actual[0].Properties.ContainsKey("nullValue"));
            Assert.Equal(JsonNull.Instance, actual[0].Properties["nullValue"]);
        }

        [Fact]
        public void TestCompatScreenAcceptNullDict()
        {
            var actual = new List<ScreenEvent>();
            _plugin.Setup(o => o.Screen(Capture.In(actual)));
            _analytics.Add(_plugin.Object);
            _analytics.Screen("user123", "foo", null);

            Assert.NotEmpty(actual);
            Assert.False(actual[0].Properties.ContainsKey("userId"));
            Assert.Equal("user123", actual[0].UserId);
            Assert.Equal("foo", actual[0].Name);
        }

        [Fact]
        public void TestCompatScreenAcceptDictWithNulls()
        {
            var properties = new Dictionary<string, object>
            {
                ["nullValue"] = null
            };
            var actual = new List<ScreenEvent>();
            _plugin.Setup(o => o.Screen(Capture.In(actual)));
            _analytics.Add(_plugin.Object);
            _analytics.Screen("user123", "foo", properties);

            Assert.NotEmpty(actual);
            Assert.False(actual[0].Properties.ContainsKey("userId"));
            Assert.Equal("user123", actual[0].UserId);
            Assert.Equal("foo", actual[0].Name);
            Assert.True(actual[0].Properties.ContainsKey("nullValue"));
            Assert.Equal(JsonNull.Instance, actual[0].Properties["nullValue"]);
        }

        [Fact]
        public void TestCompatPageAcceptNullDict()
        {
            var actual = new List<PageEvent>();
            _plugin.Setup(o => o.Page(Capture.In(actual)));
            _analytics.Add(_plugin.Object);
            _analytics.Page("user123", "foo", null);

            Assert.NotEmpty(actual);
            Assert.False(actual[0].Properties.ContainsKey("userId"));
            Assert.Equal("user123", actual[0].UserId);
            Assert.Equal("foo", actual[0].Name);
        }

        [Fact]
        public void TestCompatPageAcceptDictWithNulls()
        {
            var properties = new Dictionary<string, object>
            {
                ["nullValue"] = null
            };
            var actual = new List<PageEvent>();
            _plugin.Setup(o => o.Page(Capture.In(actual)));
            _analytics.Add(_plugin.Object);
            _analytics.Page("user123", "foo", properties);

            Assert.NotEmpty(actual);
            Assert.False(actual[0].Properties.ContainsKey("userId"));
            Assert.Equal("user123", actual[0].UserId);
            Assert.Equal("foo", actual[0].Name);
            Assert.True(actual[0].Properties.ContainsKey("nullValue"));
            Assert.Equal(JsonNull.Instance, actual[0].Properties["nullValue"]);
        }

        [Fact]
        public void TestCompatGroupAcceptNullDict()
        {
            var actual = new List<GroupEvent>();
            _plugin.Setup(o => o.Group(Capture.In(actual)));
            _analytics.Add(_plugin.Object);
            _analytics.Group("user123", "foo", null);

            Assert.NotEmpty(actual);
            Assert.False(actual[0].Traits.ContainsKey("userId"));
            Assert.Equal("user123", actual[0].UserId);
            Assert.Equal("foo", actual[0].GroupId);
        }

        [Fact]
        public void TestCompatGroupAcceptDictWithNulls()
        {
            var properties = new Dictionary<string, object>
            {
                ["nullValue"] = null
            };
            var actual = new List<GroupEvent>();
            _plugin.Setup(o => o.Group(Capture.In(actual)));
            _analytics.Add(_plugin.Object);
            _analytics.Group("user123", "foo", properties);

            Assert.NotEmpty(actual);
            Assert.False(actual[0].Traits.ContainsKey("userId"));
            Assert.Equal("user123", actual[0].UserId);
            Assert.Equal("foo", actual[0].GroupId);
            Assert.True(actual[0].Traits.ContainsKey("nullValue"));
            Assert.Equal(JsonNull.Instance, actual[0].Traits["nullValue"]);
        }
    }
}
