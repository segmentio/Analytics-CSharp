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
                this.Analytics._userInfo._userId = identifyEvent.UserId ?? this.Analytics._userInfo._userId;
                this.Analytics._userInfo._traits = identifyEvent.Traits ?? this.Analytics._userInfo._traits;
            }
            else if (@event is AliasEvent aliasEvent)
            {
                this.Analytics._userInfo._userId = aliasEvent.UserId ?? this.Analytics._userInfo._userId;
            }
            else
            {
                @event.AnonymousId = this.Analytics._userInfo._anonymousId;
                @event.UserId = this.Analytics._userInfo._userId;
            }
        }

        public override RawEvent Execute(RawEvent incomingEvent)
        {
            this.ApplyUserInfoData(incomingEvent);
            return base.Execute(incomingEvent);
        }
    }
}
