using Segment.Serialization;
using Segment.Sovran;

namespace Segment.Analytics.Plugins
{
    /// <summary>
    /// Analytics plugin used to populate events with basic UserInfo data.
    /// Auto-added to analytics client on construction
    /// </summary>
    public class UserInfoPlugin : Plugin
    {
        public override PluginType type => PluginType.Before;

        private Store store;

        public override void Configure(Analytics analytics)
        {
            base.Configure(analytics);
            store = analytics.store;
        }

        private void ApplyUserInfoData(RawEvent @event)
        {
            if (@event is IdentifyEvent identifyEvent)
            {
                if (identifyEvent.userId != null)
                {
                    analytics.userInfo.userId = identifyEvent.userId;
                }
                if (identifyEvent.traits != null)
                {
                    analytics.userInfo.traits = identifyEvent.traits;
                }
            }
            else if (@event is AliasEvent aliasEvent)
            {
                if (aliasEvent.userId != null)
                analytics.userInfo.userId = aliasEvent.userId;
            }
            else
            {
                @event.anonymousId = analytics.userInfo.anonymousId;
                @event.userId = analytics.userInfo.userId;
            }
        }

        public override RawEvent Execute(RawEvent incomingEvent)
        {
            ApplyUserInfoData(incomingEvent);
            return base.Execute(incomingEvent);
        }
    }
}