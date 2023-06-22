using Moq;
using Segment.Analytics;
using Segment.Analytics.Plugins;
using Segment.Analytics.Utilities;
using Segment.Serialization;
using Tests.Utils;
using Xunit;

namespace Tests.Plugins
{
    public class DestinationMetadataPluginTest
    {
        private readonly Analytics _analytics;

        private Settings _settings;

        private DestinationMetadataPlugin _metadataPlugin;

        public DestinationMetadataPluginTest()
        {
            _settings = JsonUtility.FromJson<Settings>(
                "{\"integrations\":{\"Segment.io\":{\"apiKey\":\"1vNgUqwJeCHmqgI9S1sOm9UHCyfYqbaQ\"}},\"plan\":{},\"edgeFunction\":{}}");

            var mockHttpClient = new Mock<HTTPClient>(null, null, null);
            mockHttpClient
                .Setup(httpClient => httpClient.Settings())
                .ReturnsAsync(_settings);

            var config = new Configuration(
                writeKey: "123",
                storageProvider: new DefaultStorageProvider("tests"),
                autoAddSegmentDestination: false,
                userSynchronizeDispatcher: true,
                httpClientProvider: new MockHttpClientProvider(mockHttpClient)
            );
            _analytics = new Analytics(config);
            _metadataPlugin = new DestinationMetadataPlugin();
            _metadataPlugin.Configure(_analytics);
            _metadataPlugin.Update(_settings, UpdateType.Initial);
        }

        [Fact]
        public void TestBundled()
        {
            var a = new StubDestinationPlugin("A");
            var b = new StubDestinationPlugin("B");
            var c = new StubDestinationPlugin("C");
            _analytics.Add(a);
            _analytics.Add(b);
            _analytics.Add(c);
            _analytics.ManuallyEnableDestination(a);
            _analytics.ManuallyEnableDestination(b);
            _analytics.ManuallyEnableDestination(c);

            var trackEvent = new TrackEvent("test", new JsonObject());
            RawEvent actual = _metadataPlugin.Execute(trackEvent);

            Assert.Equal(3, actual._metadata.Bundled.Count);
            Assert.Equal(0, actual._metadata.Unbundled.Count);
            Assert.Equal(0, actual._metadata.BundledIds.Count);
        }

        [Fact]
        public void TestIntegrationNotInBundled()
        {
            _settings.Integrations.Add("a", "test");
            _settings.Integrations.Add("b", "test");
            _settings.Integrations.Add("c", "test");
            _metadataPlugin.Update(_settings, UpdateType.Refresh);

            var trackEvent = new TrackEvent("test", new JsonObject());
            RawEvent actual = _metadataPlugin.Execute(trackEvent);

            Assert.Equal(0, actual._metadata.Bundled.Count);
            Assert.Equal(3, actual._metadata.Unbundled.Count);
            Assert.Equal(0, actual._metadata.BundledIds.Count);
        }

        [Fact]
        public void TestUnbundledIntegrations()
        {
            _metadataPlugin.Update(new Settings
            {
                Integrations = new JsonObject
                {
                    ["Segment.io"] = new JsonObject
                    {
                        ["unbundledIntegrations"] = new JsonArray
                        {
                            "a", "b", "c"
                        }
                    }
                }
            }, UpdateType.Refresh);

            var trackEvent = new TrackEvent("test", new JsonObject());
            RawEvent actual = _metadataPlugin.Execute(trackEvent);

            Assert.Equal(0, actual._metadata.Bundled.Count);
            Assert.Equal(3, actual._metadata.Unbundled.Count);
            Assert.Equal(0, actual._metadata.BundledIds.Count);
        }

        [Fact]
        public void TestCombination()
        {
            // bundled
            var a = new StubDestinationPlugin("A");
            _analytics.Add(a);
            _analytics.ManuallyEnableDestination(a);


            _metadataPlugin.Update(new Settings
            {
                Integrations = new JsonObject
                {
                    // IntegrationNotInBundled
                    ["b"] = "test",
                    ["c"] = "test",
                    ["Segment.io"] = new JsonObject
                    {
                        ["unbundledIntegrations"] = new JsonArray
                        {
                            // UnbundledIntegrations
                            "d", "e", "f"
                        }
                    }
                }
            }, UpdateType.Refresh);

            var trackEvent = new TrackEvent("test", new JsonObject());
            RawEvent actual = _metadataPlugin.Execute(trackEvent);

            Assert.Equal(1, actual._metadata.Bundled.Count);
            Assert.Equal(5, actual._metadata.Unbundled.Count);
            Assert.Equal(0, actual._metadata.BundledIds.Count);
        }
    }
}
