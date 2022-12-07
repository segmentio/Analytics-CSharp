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
            var userInfoTask = store.CurrentState<UserInfo>();
            userInfoTask.Wait();
            var userInfo = userInfoTask.Result;

            if (@event is IdentifyEvent identifyEvent)
            {
            }
            else if (@event is AliasEvent aliasEvent)
            {
            }
            else
            {
                @event.anonymousId = userInfo.anonymousId;
                @event.userId = userInfo.userId;
            }
        }

        public override RawEvent Execute(RawEvent incomingEvent)
        {
            ApplyUserInfoData(incomingEvent);
            return base.Execute(incomingEvent);
        }
    }
}