using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Segment.Analytics;
using Segment.Analytics.Utilities;
using Segment.Serialization;
using Segment.Sovran;
using Tests.Utils;
using Xunit;

namespace Tests.Utilities
{
    public class SettingsTest
    {
        private readonly Analytics _analytics;

        private Mock<IStorage> _storage;

        private Settings? _settings;

        public SettingsTest()
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
        public async Task PluginUpdatesWithInitalOnlyOnce()
        {
            var plugin = new Mock<DestinationPlugin>();
            plugin.Setup(o => o.Key).Returns("mock");
            plugin.Setup(o => o.Analytics).Returns(_analytics); // This would normally be set by Configure

            // Ideally we'd interrupt init somehow to test adding plugins before, but we don't have a good way
            _analytics.Add(plugin.Object);
            plugin.Verify(p => p.Update(It.IsAny<Settings>(), UpdateType.Initial), Times.Once);
            plugin.Verify(p => p.Update(It.IsAny<Settings>(), UpdateType.Refresh), Times.Never);

            // load settings
            await _analytics.CheckSettings();
            plugin.Verify(p => p.Update(It.IsAny<Settings>(), UpdateType.Initial), Times.Once);
            plugin.Verify(p => p.Update(It.IsAny<Settings>(), UpdateType.Refresh), Times.Once);
            Segment.Analytics.System system = await _analytics.Store.CurrentState<Segment.Analytics.System>();
            Assert.Contains(plugin.Object.GetHashCode(), system._initializedPlugins);

            // readd plugin (why would you do this?)
            _analytics.Remove(plugin.Object);
            _analytics.Add(plugin.Object);
            plugin.Verify(p => p.Update(It.IsAny<Settings>(), UpdateType.Initial), Times.Exactly(2));
            plugin.Verify(p => p.Update(It.IsAny<Settings>(), UpdateType.Refresh), Times.Once);

            // load settings again
            await _analytics.CheckSettings();
            plugin.Verify(p => p.Update(It.IsAny<Settings>(), UpdateType.Initial), Times.Exactly(2));
            plugin.Verify(p => p.Update(It.IsAny<Settings>(), UpdateType.Refresh), Times.Exactly(2));
        }
    }
}