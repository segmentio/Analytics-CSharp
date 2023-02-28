namespace Segment.Analytics.Plugins
{
    /// <summary>
    /// Analytics plugin used to populate events with basic UserInfo data.
    /// Auto-added to analytics client on construction
    /// </summary>
    public class UserInfoPlugin : Plugin
    {
        public override PluginType Type => PluginType.Before;

        public override void Configure(Analytics analytics) => base.Configure(analytics);

        private void ApplyUserInfoData(RawEvent @event)
        {
            if (@event is IdentifyEvent identifyEvent)
            {
                Analytics._userInfo._userId = identifyEvent.UserId ?? Analytics._userInfo._userId;
                Analytics._userInfo._traits = identifyEvent.Traits ?? Analytics._userInfo._traits;
            }
            else if (@event is AliasEvent aliasEvent)
            {
                Analytics._userInfo._userId = aliasEvent.UserId ?? Analytics._userInfo._userId;
            }
            else
            {
                @event.AnonymousId = Analytics._userInfo._anonymousId;
                @event.UserId = Analytics._userInfo._userId;
            }
        }

        public override RawEvent Execute(RawEvent incomingEvent)
        {
            ApplyUserInfoData(incomingEvent);
            return base.Execute(incomingEvent);
        }
    }
}
