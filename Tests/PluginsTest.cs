using System.Linq;
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
    }
}
