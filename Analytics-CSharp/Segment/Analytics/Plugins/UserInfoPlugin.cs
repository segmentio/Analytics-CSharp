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

        public override void Configure(Analytics analytics)
        {
            base.Configure(analytics);
        }

        private void ApplyUserInfoData(RawEvent @event)
        {
            if (@event is IdentifyEvent identifyEvent)
            {
                analytics.userInfo.userId = identifyEvent.userId ?? analytics.userInfo.userId;
                analytics.userInfo.traits = identifyEvent.traits ?? analytics.userInfo.traits;
            }
            else if (@event is AliasEvent aliasEvent)
            {
                analytics.userInfo.userId = aliasEvent.userId ?? analytics.userInfo.userId;
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