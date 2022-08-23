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
        private Analytics _analytics;

        private Settings? _settings;
        
        private Mock<StubEventPlugin> _plugin;

        public EventsTest()
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
            var expectedEvent = "foo";
            var actual = new List<TrackEvent>();
            _plugin.Setup(o => o.Track(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Track(expectedEvent, expected);
            
            Assert.NotEmpty(actual);
            Assert.Equal(expected, actual[0].properties);
            Assert.Equal(expectedEvent, actual[0].@event);
        }
        
        [Fact]
        public void TestTrackNoProperties()
        {
            var expectedEvent = "foo";
            var actual = new List<TrackEvent>();
            _plugin.Setup(o => o.Track(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Track(expectedEvent);
            
            Assert.NotEmpty(actual);
            Assert.True(actual[0].properties.count == 0);
            Assert.Equal(expectedEvent, actual[0].@event);
        }
        
        [Fact]
        public void TestTrackT()
        {
            var expected = new FooBar();
            var expectedEvent = "foo";
            var actual = new List<TrackEvent>();
            _plugin.Setup(o => o.Track(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Track(expectedEvent, expected);
            
            Assert.NotEmpty(actual);
            Assert.Equal(expected.GetJsonObject(), actual[0].properties);
            Assert.Equal(expectedEvent, actual[0].@event);
        }
        
        [Fact]
        public void TestTrackTNoProperties()
        {
            var expectedEvent = "foo";
            var actual = new List<TrackEvent>();
            _plugin.Setup(o => o.Track(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Track<FooBar>(expectedEvent);
            
            Assert.NotEmpty(actual);
            Assert.True(actual[0].properties.count == 0);
            Assert.Equal(expectedEvent, actual[0].@event);
        }

        [Fact]
        public async Task TestIdentify()
        {
            var expected = new JsonObject
            {
                ["foo"] = "bar"
            };
            var expectedUserId = "newUserId";
            var actual = new List<IdentifyEvent>();
            _plugin.Setup(o => o.Identify(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Identify(expectedUserId, expected);
            var actualUserId = await _analytics.UserIdAsync();
            
            Assert.NotEmpty(actual);
            Assert.Equal(expected, actual[0].traits);
            Assert.Equal(expectedUserId, actualUserId);
        }
        
        [Fact]
        public async Task TestIdentifyNoTraits()
        {
            var expectedUserId = "newUserId";
            var actual = new List<IdentifyEvent>();
            _plugin.Setup(o => o.Identify(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Identify(expectedUserId);
            var actualUserId = await _analytics.UserIdAsync();
            
            Assert.NotEmpty(actual);
            Assert.True(actual[0].traits.count == 0);
            Assert.Equal(expectedUserId, actualUserId);
        }
        
        [Fact]
        public async Task TestIdentifyT()
        {
            var expected = new FooBar();
            var expectedUserId = "newUserId";
            var actual = new List<IdentifyEvent>();
            _plugin.Setup(o => o.Identify(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Identify(expectedUserId, expected);
            var actualUserId = await _analytics.UserIdAsync();
            
            Assert.NotEmpty(actual);
            Assert.Equal(expected.GetJsonObject(), actual[0].traits);
            Assert.Equal(expectedUserId, actualUserId);
        }
        
        [Fact]
        public async Task TestIdentifyTNoTraits()
        {
            var expectedUserId = "newUserId";
            var actual = new List<IdentifyEvent>();
            _plugin.Setup(o => o.Identify(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Identify<FooBar>(expectedUserId);
            var actualUserId = await _analytics.UserIdAsync();
            
            Assert.NotEmpty(actual);
            Assert.True(actual[0].traits.count == 0);
            Assert.Equal(expectedUserId, actualUserId);
        }

        [Fact]
        public void TestScreen()
        {
            var expected = new JsonObject
            {
                ["foo"] = "bar"
            };
            var expectedTitle = "foo";
            var expectedCategory = "bar";
            var actual = new List<ScreenEvent>();
            _plugin.Setup(o => o.Screen(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Screen(expectedTitle, expected, expectedCategory);
            
            Assert.NotEmpty(actual);
            Assert.Equal(expected, actual[0].properties);
            Assert.Equal(expectedTitle, actual[0].name);
            Assert.Equal(expectedCategory, actual[0].category);
        }
        
        [Fact]
        public void TestScreenWithNulls()
        {
            var actual = new List<ScreenEvent>();
            _plugin.Setup(o => o.Screen(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Screen(null, null, null);
            
            Assert.NotEmpty(actual);
            Assert.True(actual[0].properties.count == 0);
            Assert.Null(actual[0].name);
            Assert.Null(actual[0].category);
        }

        [Fact]
        public void TestScreenT()
        {
            var expected = new FooBar();
            var expectedTitle = "foo";
            var expectedCategory = "bar";
            var actual = new List<ScreenEvent>();
            _plugin.Setup(o => o.Screen(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Screen(expectedTitle, expected, expectedCategory);
            
            Assert.NotEmpty(actual);
            Assert.Equal(expected.GetJsonObject(), actual[0].properties);
            Assert.Equal(expectedTitle, actual[0].name);
            Assert.Equal(expectedCategory, actual[0].category);
        }

        [Fact]
        public void TestScreenTWithNulls()
        {
            var actual = new List<ScreenEvent>();
            _plugin.Setup(o => o.Screen(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Screen<FooBar>(null, null, null);
            
            Assert.NotEmpty(actual);
            Assert.True(actual[0].properties.count == 0);
            Assert.Null(actual[0].name);
            Assert.Null(actual[0].category);
        }

        [Fact]
        public void TestGroup()
        {
            var expected = new JsonObject
            {
                ["foo"] = "bar"
            };
            var expectedGroupId = "foo";
            var actual = new List<GroupEvent>();
            _plugin.Setup(o => o.Group(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Group(expectedGroupId, expected);
            
            Assert.NotEmpty(actual);
            Assert.Equal(expected, actual[0].traits);
            Assert.Equal(expectedGroupId, actual[0].groupId);
        }

        [Fact]
        public void TestGroupNoProperties()
        {
            var expectedGroupId = "foo";
            var actual = new List<GroupEvent>();
            _plugin.Setup(o => o.Group(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Group(expectedGroupId);
            
            Assert.NotEmpty(actual);
            Assert.True(actual[0].traits.count == 0);
            Assert.Equal(expectedGroupId, actual[0].groupId);
        }
        
        [Fact]
        public void TestGroupT()
        {
            var expected = new FooBar();
            var expectedGroupId = "foo";
            var actual = new List<GroupEvent>();
            _plugin.Setup(o => o.Group(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Group(expectedGroupId, expected);
            
            Assert.NotEmpty(actual);
            Assert.Equal(expected.GetJsonObject(), actual[0].traits);
            Assert.Equal(expectedGroupId, actual[0].groupId);
        }
        
        [Fact]
        public void TestGroupTNoProperties()
        {
            var expectedGroupId = "foo";
            var actual = new List<GroupEvent>();
            _plugin.Setup(o => o.Group(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Group<FooBar>(expectedGroupId);
            
            Assert.NotEmpty(actual);
            Assert.True(actual[0].traits.count == 0);
            Assert.Equal(expectedGroupId, actual[0].groupId);
        }

        [Fact]
        public void TestAlias()
        {
            var expectedPrevious = "foo";
            var expected = "bar";
            var actual = new List<AliasEvent>();
            _plugin.Setup(o => o.Alias(Capture.In(actual)));

            _analytics.Add(_plugin.Object);
            _analytics.Identify(expectedPrevious);
            _analytics.Alias(expected);
            
            Assert.NotEmpty(actual);
            Assert.Equal(expectedPrevious, actual[0].previousId);
            Assert.Equal(expected, actual[0].userId);
        }
    }
}