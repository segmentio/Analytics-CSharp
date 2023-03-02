using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Segment.Analytics;
using Segment.Analytics.Utilities;
using Segment.Serialization;
using Tests.Utils;
using Xunit;

namespace Tests
{
    public class EventsTest
    {
        private readonly Analytics _analytics;

        private Settings? _settings;

        private readonly Mock<StubEventPlugin> _plugin;

        public EventsTest()
        {
            _settings = JsonUtility.FromJson<Settings?>(
                "{\"integrations\":{\"Segment.io\":{\"apiKey\":\"1vNgUqwJeCHmqgI9S1sOm9UHCyfYqbaQ\"}},\"plan\":{},\"edgeFunction\":{}}");

            var config = new Configuration(
                writeKey: "123",
                storageProvider: new DefaultStorageProvider("tests"),
                autoAddSegmentDestination: false,
                userSynchronizeDispatcher: true
            );

            var mockHttpClient = new Mock<HTTPClient>(null, null, null);
            mockHttpClient
                .Setup(httpClient => httpClient.Settings())
                .ReturnsAsync(_settings);

            _plugin = new Mock<StubEventPlugin>
            {
                CallBase = true
            };

            _analytics = new Analytics(config, httpClient: mockHttpClient.Object);
        }

        [Fact]
        public void TestTrack()
        {
            var expected = new JsonObject
            {
                ["foo"] = "bar"
            };
            string expectedEvent = "foo";
            var actual = new List<TrackEvent>();
            _plugin.Setup(o => o.Track(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Track(expectedEvent, expected);

            Assert.NotEmpty(actual);
            Assert.Equal(expected, actual[0].Properties);
            Assert.Equal(expectedEvent, actual[0].Event);
        }

        [Fact]
        public void TestTrackNoProperties()
        {
            string expectedEvent = "foo";
            var actual = new List<TrackEvent>();
            _plugin.Setup(o => o.Track(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Track(expectedEvent);

            Assert.NotEmpty(actual);
            Assert.True(actual[0].Properties.Count == 0);
            Assert.Equal(expectedEvent, actual[0].Event);
        }

        [Fact]
        public void TestTrackT()
        {
            var expected = new FooBar();
            string expectedEvent = "foo";
            var actual = new List<TrackEvent>();
            _plugin.Setup(o => o.Track(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Track(expectedEvent, expected);

            Assert.NotEmpty(actual);
            Assert.Equal(expected.GetJsonObject(), actual[0].Properties);
            Assert.Equal(expectedEvent, actual[0].Event);
        }

        [Fact]
        public void TestTrackTNoProperties()
        {
            string expectedEvent = "foo";
            var actual = new List<TrackEvent>();
            _plugin.Setup(o => o.Track(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Track<FooBar>(expectedEvent);

            Assert.NotEmpty(actual);
            Assert.True(actual[0].Properties.Count == 0);
            Assert.Equal(expectedEvent, actual[0].Event);
        }

        [Fact]
        public void TestIdentify()
        {
            var expected = new JsonObject
            {
                ["foo"] = "bar"
            };
            string expectedUserId = "newUserId";
            var actual = new List<IdentifyEvent>();
            _plugin.Setup(o => o.Identify(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Identify(expectedUserId, expected);

            string actualUserId = _analytics.UserId();

            Assert.NotEmpty(actual);
            Assert.Equal(expected, actual[0].Traits);
            Assert.Equal(expectedUserId, actualUserId);
        }

        [Fact]
        public void TestIdentifyNoTraits()
        {
            string expectedUserId = "newUserId";
            var actual = new List<IdentifyEvent>();
            _plugin.Setup(o => o.Identify(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Identify(expectedUserId);

            string actualUserId = _analytics.UserId();

            Assert.NotEmpty(actual);
            Assert.True(actual[0].Traits.Count == 0);
            Assert.Equal(expectedUserId, actualUserId);
        }

        [Fact]
        public void TestIdentifyNoUserId()
        {
            var expected = new JsonObject
            {
                ["foo"] = "bar"
            };
            var actual = new List<IdentifyEvent>();
            _plugin.Setup(o => o.Identify(Capture.In(actual)));
            string expectedUserId = _analytics.UserId();

            _analytics.Add(_plugin.Object);
            _analytics.Identify(expected);

            string actualUserId = _analytics.UserId();

            Assert.NotEmpty(actual);
            Assert.Equal(expected, actual[0].Traits);
            Assert.Equal(expectedUserId, actualUserId);
        }

        [Fact]
        public void TestIdentifyT()
        {
            var expected = new FooBar();
            string expectedUserId = "newUserId";
            var actual = new List<IdentifyEvent>();
            _plugin.Setup(o => o.Identify(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Identify(expectedUserId, expected);

            string actualUserId = _analytics.UserId();

            Assert.NotEmpty(actual);
            Assert.Equal(expected.GetJsonObject(), actual[0].Traits);
            Assert.Equal(expectedUserId, actualUserId);
        }

        [Fact]
        public void TestIdentifyTNoTraits()
        {
            string expectedUserId = "newUserId";
            var actual = new List<IdentifyEvent>();
            _plugin.Setup(o => o.Identify(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Identify<FooBar>(expectedUserId);
            string actualUserId = _analytics.UserId();

            Assert.NotEmpty(actual);
            Assert.True(actual[0].Traits.Count == 0);
            Assert.Equal(expectedUserId, actualUserId);
        }

        [Fact]
        public void TestIdentifyTNoUserId()
        {
            var expected = new FooBar();
            var actual = new List<IdentifyEvent>();
            _plugin.Setup(o => o.Identify(Capture.In(actual)));
            string expectedUserId = _analytics.UserId();

            _analytics.Add(_plugin.Object);
            _analytics.Identify(expected);
            string actualUserId = _analytics.UserId();

            Assert.NotEmpty(actual);
            Assert.Equal(expected.GetJsonObject(), actual[0].Traits);
            Assert.Equal(expectedUserId, actualUserId);
        }

        [Fact]
        public void TestScreen()
        {
            var expected = new JsonObject
            {
                ["foo"] = "bar"
            };
            string expectedTitle = "foo";
            string expectedCategory = "bar";
            var actual = new List<ScreenEvent>();
            _plugin.Setup(o => o.Screen(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Screen(expectedTitle, expected, expectedCategory);

            Assert.NotEmpty(actual);
            Assert.Equal(expected, actual[0].Properties);
            Assert.Equal(expectedTitle, actual[0].Name);
            Assert.Equal(expectedCategory, actual[0].Category);
        }

        [Fact]
        public void TestScreenWithNulls()
        {
            var actual = new List<ScreenEvent>();
            _plugin.Setup(o => o.Screen(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Screen(null, null, null);

            Assert.NotEmpty(actual);
            Assert.True(actual[0].Properties.Count == 0);
            Assert.Null(actual[0].Name);
            Assert.Null(actual[0].Category);
        }

        [Fact]
        public void TestScreenT()
        {
            var expected = new FooBar();
            string expectedTitle = "foo";
            string expectedCategory = "bar";
            var actual = new List<ScreenEvent>();
            _plugin.Setup(o => o.Screen(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Screen(expectedTitle, expected, expectedCategory);

            Assert.NotEmpty(actual);
            Assert.Equal(expected.GetJsonObject(), actual[0].Properties);
            Assert.Equal(expectedTitle, actual[0].Name);
            Assert.Equal(expectedCategory, actual[0].Category);
        }

        [Fact]
        public void TestScreenTWithNulls()
        {
            var actual = new List<ScreenEvent>();
            _plugin.Setup(o => o.Screen(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Screen<FooBar>(null, null, null);

            Assert.NotEmpty(actual);
            Assert.True(actual[0].Properties.Count == 0);
            Assert.Null(actual[0].Name);
            Assert.Null(actual[0].Category);
        }

        [Fact]
        public void TestGroup()
        {
            var expected = new JsonObject
            {
                ["foo"] = "bar"
            };
            string expectedGroupId = "foo";
            var actual = new List<GroupEvent>();
            _plugin.Setup(o => o.Group(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Group(expectedGroupId, expected);

            Assert.NotEmpty(actual);
            Assert.Equal(expected, actual[0].Traits);
            Assert.Equal(expectedGroupId, actual[0].GroupId);
        }

        [Fact]
        public void TestGroupNoProperties()
        {
            string expectedGroupId = "foo";
            var actual = new List<GroupEvent>();
            _plugin.Setup(o => o.Group(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Group(expectedGroupId);

            Assert.NotEmpty(actual);
            Assert.True(actual[0].Traits.Count == 0);
            Assert.Equal(expectedGroupId, actual[0].GroupId);
        }

        [Fact]
        public void TestGroupT()
        {
            var expected = new FooBar();
            string expectedGroupId = "foo";
            var actual = new List<GroupEvent>();
            _plugin.Setup(o => o.Group(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Group(expectedGroupId, expected);

            Assert.NotEmpty(actual);
            Assert.Equal(expected.GetJsonObject(), actual[0].Traits);
            Assert.Equal(expectedGroupId, actual[0].GroupId);
        }

        [Fact]
        public void TestGroupTNoProperties()
        {
            string expectedGroupId = "foo";
            var actual = new List<GroupEvent>();
            _plugin.Setup(o => o.Group(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Group<FooBar>(expectedGroupId);

            Assert.NotEmpty(actual);
            Assert.True(actual[0].Traits.Count == 0);
            Assert.Equal(expectedGroupId, actual[0].GroupId);
        }

        [Fact]
        public void TestAlias()
        {
            string expectedPrevious = "foo";
            string expected = "bar";
            var actual = new List<AliasEvent>();
            _plugin.Setup(o => o.Alias(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Identify(expectedPrevious);
            _analytics.Alias(expected);

            Assert.NotEmpty(actual);
            Assert.Equal(expectedPrevious, actual[0].PreviousId);
            Assert.Equal(expected, actual[0].UserId);
        }
    }
}
