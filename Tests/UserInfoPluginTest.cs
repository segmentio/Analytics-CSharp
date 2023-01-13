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
    public class UserInfoPluginTest
    {
        private Analytics _analytics;

        private Settings? _settings;

        public UserInfoPluginTest()
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
        public void TestIdentify()
        {
            UserInfoPlugin userInfoPlugin = new UserInfoPlugin();
            IdentifyEvent identifyEvent = new IdentifyEvent("bob");

            userInfoPlugin.Configure(_analytics);
            userInfoPlugin.Execute(identifyEvent);

            TrackEvent trackEvent = new TrackEvent("eventname",null);
            userInfoPlugin.Execute(trackEvent);

            Assert.Equal("bob", trackEvent.userId);
		}


        [Fact]
        public void TestAlias()
        {
            UserInfoPlugin userInfoPlugin = new UserInfoPlugin();
            AliasEvent aliasEvent = new AliasEvent("steve","bob");

            userInfoPlugin.Configure(_analytics);
            userInfoPlugin.Execute(aliasEvent);

            TrackEvent trackEvent = new TrackEvent("eventname", null);
            userInfoPlugin.Execute(trackEvent);

            Assert.Equal("steve", trackEvent.userId);
        }
    }
}

