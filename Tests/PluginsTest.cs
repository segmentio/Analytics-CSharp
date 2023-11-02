using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Moq;
using Segment.Analytics;
using Segment.Analytics.Plugins;
using Segment.Analytics.Utilities;
using Segment.Serialization;
using Tests.Utils;
using Xunit;

namespace Tests
{
    public class PluginsTest
    {

        private readonly Analytics _analytics;

        private Settings? _settings;

        public PluginsTest()
        {
            _settings = JsonUtility.FromJson<Settings?>(
                "{\"integrations\":{\"Segment.io\":{\"apiKey\":\"1vNgUqwJeCHmqgI9S1sOm9UHCyfYqbaQ\"}},\"plan\":{},\"edgeFunction\":{}}");

            var mockHttpClient = new Mock<HTTPClient>(null, null, null);
            mockHttpClient
                .Setup(httpClient => httpClient.Settings())
                .ReturnsAsync(_settings);

            var config = new Configuration(
                writeKey: "123",
                storageProvider: new DefaultStorageProvider("tests"),
                autoAddSegmentDestination: false,
                useSynchronizeDispatcher: true,
                httpClientProvider: new MockHttpClientProvider(mockHttpClient)
            );
            _analytics = new Analytics(config);
        }

        [Fact]
        public void TestApply()
        {
            int expected = _analytics.Timeline._plugins.Sum(o => o.Value._plugins.Count);

            int actual = 0;
            _analytics.Apply(_ =>
            {
                actual++;
            });

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TestAdd()
        {
            var plugin = new StubEventPlugin();

            _analytics.Add(plugin);

            Assert.Equal(_analytics, plugin.Analytics);
            Assert.Contains(plugin, _analytics.Timeline._plugins[PluginType.Before]._plugins);
        }

        [Fact]
        public void TestRemove()
        {
            Plugin plugin = _analytics.Timeline._plugins[PluginType.Before]._plugins[0];

            _analytics.Remove(plugin);

            Assert.DoesNotContain(plugin, _analytics.Timeline._plugins[PluginType.Before]._plugins);
        }

        [Fact]
        public void TestFind()
        {
            var expected = new StubEventPlugin();
            _analytics.Add(expected);
            _analytics.Add(new StubEventPlugin());

            StubEventPlugin actual = _analytics.Find<StubEventPlugin>();

            // should be the same as the the first match
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TestFindAll()
        {
            _analytics.Add(new StubEventPlugin());
            _analytics.Add(new StubEventPlugin());

            System.Collections.Generic.IEnumerable<StubEventPlugin> actual = _analytics.FindAll<StubEventPlugin>();

            Assert.Equal(2, actual.Count());
        }

        [Fact]
        public void FindByDestinationKey()
        {
            var expected = new SegmentDestination();
            _analytics.Add(expected);

            DestinationPlugin actual = _analytics.Find(expected.Key);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TestDestinationPluginProcess()
        {
            var plugin = new Mock<DestinationPlugin>
            {
                // need this setting to prevent faking internal methods
                CallBase = true
            };
            plugin.Setup(o => o.Key).Returns("mock");

            var alias = new List<AliasEvent>();
            plugin.Setup(o => o.Alias(Moq.Capture.In(alias))).Returns((AliasEvent a) => {return a;}).Verifiable();
            var group = new List<GroupEvent>();
            plugin.Setup(o => o.Group(Moq.Capture.In(group))).Returns((GroupEvent a) => {return a;}).Verifiable();
            var identify = new List<IdentifyEvent>();
            plugin.Setup(o => o.Identify(Moq.Capture.In(identify))).Returns((IdentifyEvent a) => {return a;}).Verifiable();
            var page = new List<PageEvent>();
            plugin.Setup(o => o.Page(Moq.Capture.In(page))).Returns((PageEvent a) => {return a;}).Verifiable();
            var screen = new List<ScreenEvent>();
            plugin.Setup(o => o.Screen(Moq.Capture.In(screen))).Returns((ScreenEvent a) => {return a;}).Verifiable();
            var track = new List<TrackEvent>();
            plugin.Setup(o => o.Track(Moq.Capture.In(track))).Returns((TrackEvent a) => {return a;}).Verifiable();
            
            _analytics.Add(plugin.Object);
            _analytics.ManuallyEnableDestination(plugin.Object);

            _analytics.Alias("testalias");
            plugin.Verify(o => o.Alias(It.IsAny<AliasEvent>()), Times.Exactly(1));
            Assert.Equal("testalias", alias[0].UserId);

            _analytics.Group("testgroup");
            plugin.Verify(o => o.Group(It.IsAny<GroupEvent>()), Times.Exactly(1));
            Assert.Equal("testgroup", group[0].GroupId);

            _analytics.Identify("testidentify");
            plugin.Verify(o => o.Identify(It.IsAny<IdentifyEvent>()), Times.Exactly(1));
            Assert.Equal("testidentify", identify[0].UserId);

            _analytics.Page("testpage");
            plugin.Verify(o => o.Page(It.IsAny<PageEvent>()), Times.Exactly(1));
            Assert.Equal("testpage", page[0].Name);

            _analytics.Screen("testscreen");
            plugin.Verify(o => o.Screen(It.IsAny<ScreenEvent>()), Times.Exactly(1));
            Assert.Equal("testscreen", screen[0].Name);

            _analytics.Track("testtrack");
            plugin.Verify(o => o.Track(It.IsAny<TrackEvent>()), Times.Exactly(1));
            Assert.Equal("testtrack", track[0].Event);
            
        }
    }
}
