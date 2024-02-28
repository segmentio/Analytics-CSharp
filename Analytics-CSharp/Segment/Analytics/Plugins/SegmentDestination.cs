using System;
using Segment.Analytics.Utilities;
using Segment.Serialization;
using Segment.Sovran;

namespace Segment.Analytics.Plugins
{
    /// <summary>
    /// Segment Analytics plugin that is used to send events to Segment's tracking api, in the choice of region.
    /// How it works:
    /// <list type="number">
    /// <item><description>Plugin receives <c>apiHost</c> settings</description></item>
    /// <item><description>We store events into a file with the batch api format <see href="https://segment.com/docs/connections/sources/catalog/libraries/server/http-api/#batch" /></description></item>
    /// <item><description>We upload events on a dedicated thread using the batch api</description></item>
    /// </list>
    /// </summary>
    public class SegmentDestination : DestinationPlugin, ISubscriber
    {
        private EventPipeline _pipeline = null;

        public override string Key => "Segment.io";

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

        public override PageEvent Page(PageEvent pageEvent)
        {
            Enqueue(pageEvent);
            return pageEvent;
        }

        public override void Configure(Analytics analytics)
        {
            base.Configure(analytics);

            // Add DestinationMetadata enrichment plugin
            Add(new DestinationMetadataPlugin());

            _pipeline = new EventPipeline(
                    analytics,
                    Key,
                    analytics.Configuration.WriteKey,
                    analytics.Configuration.FlushPolicies,
                    analytics.Configuration.ApiHost,
                    analytics.Configuration.SynchroniceFlush
                );

            analytics.AnalyticsScope.Launch(analytics.AnalyticsDispatcher, async () =>
            {
                await analytics.Store.Subscribe<System>(this, state => OnEnableToggled((System)state), true);
            });
        }

        public override void Update(Settings settings, UpdateType type)
        {
            base.Update(settings, type);

            JsonObject segmentInfo = settings.Integrations?.GetJsonObject(Key);
            string apiHost = segmentInfo?.GetString(ApiHost);
            if (apiHost != null && _pipeline != null)
            {
                _pipeline.ApiHost = apiHost;
            }
        }

        public override void Reset()
        {

        }

        public override void Flush() => _pipeline?.Flush();

        public override bool FlushSync() => (bool)_pipeline?.FlushSync();

        private void Enqueue<T>(T payload) where T : RawEvent
        {
            // TODO: filter out empty userid and traits values
            _pipeline?.Put(payload);
        }

        private void OnEnableToggled(System state)
        {
            if (state._enable)
            {
                _pipeline?.Start();
            }
            else
            {
                _pipeline?.Stop();
            }
        }
    }
}
