using Segment.Serialization;
using Segment.Analytics.Utilities;
using NotImplementedException = System.NotImplementedException;

namespace Segment.Analytics.Plugins
{
    public class SegmentDestination: DestinationPlugin
    {
        private EventPipeline _pipeline;
        
        public override string key => "Segment.io";
        
        internal const string ApiHost = "apiHost";

        public override IdentifyEvent Identify(IdentifyEvent identifyEvent)
        {
            Enqueue(identifyEvent);
            return identifyEvent;
        }

        public override TrackEvent Track(TrackEvent trackEvent)
        {
            Enqueue(trackEvent);
            return trackEvent;
        }

        public override GroupEvent Group(GroupEvent groupEvent)
        {
            Enqueue(groupEvent);
            return groupEvent;
        }

        public override AliasEvent Alias(AliasEvent aliasEvent)
        {
            Enqueue(aliasEvent);
            return aliasEvent;
        }

        public override ScreenEvent Screen(ScreenEvent screenEvent)
        {
            Enqueue(screenEvent);
            return screenEvent;
        }

        public override void Configure(Analytics analytics)
        {
            base.Configure(analytics);
            
            // TODO: Add DestinationMetadata enrichment plugin

            _pipeline = new EventPipeline(
                    analytics,
                    key,
                    analytics.configuration.writeKey,
                    analytics.configuration.flushAt,
                    analytics.configuration.flushInterval * 1000L,
                    analytics.configuration.apiHost
                );
            _pipeline.Start();
        }

        public override void Update(Settings settings, UpdateType type)
        {
            base.Update(settings, type);

            var segmentInfo = settings.integrations?.GetJsonObject(key);
            var apiHost = segmentInfo?.GetString(ApiHost);
            if (apiHost != null)
            {
                _pipeline.apiHost = apiHost;
            }
        }

        public override void Reset()
        {
            throw new NotImplementedException();
        }

        public override void Flush()
        {
            _pipeline.Flush();
        }

        private void Enqueue<T>(T payload) where T : RawEvent
        {
            // TODO: filter out empty userid and traits values
            
            var str = JsonUtility.ToJson(payload);
            _pipeline.Put(str);
        }
    }
}