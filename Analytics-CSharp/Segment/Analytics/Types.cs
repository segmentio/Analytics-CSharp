namespace Segment.Analytics
{
    using global::System;
    using Segment.Serialization;

    public abstract class RawEvent
    {
        public virtual string Type { get; set; }
        public virtual string AnonymousId { get; set; }
        public virtual string MessageId { get; set; }
        public virtual string UserId { get; set; }
        public virtual string Timestamp { get; set; }

        // JSON types
        public JsonObject Context { get; set; }
        public JsonObject Integrations { get; set; }

        public JsonArray Metrics { get; set; }

        internal void ApplyRawEventData(RawEvent rawEvent)
        {
            this.AnonymousId = rawEvent.AnonymousId;
            this.MessageId = rawEvent.MessageId;
            this.UserId = rawEvent.UserId;
            this.Timestamp = rawEvent.Timestamp;
            this.Context = rawEvent.Context;
            this.Integrations = rawEvent.Integrations;
        }

        internal void ApplyRawEventData()
        {
            this.MessageId = Guid.NewGuid().ToString();
            this.Context = new JsonObject();
            this.Timestamp = DateTime.UtcNow.ToString("o"); // iso8601
            this.Integrations = new JsonObject();
        }
    }

    public sealed class TrackEvent : RawEvent
    {
        public override string Type => "track";

        public string Event { get; set; }

        public JsonObject Properties { get; set; }

        internal TrackEvent(string trackEvent, JsonObject properties)
        {
            this.Event = trackEvent;
            this.Properties = properties;
        }

        internal TrackEvent(TrackEvent existing) : this(existing.Event, existing.Properties) => this.ApplyRawEventData(existing);
    }

    public sealed class IdentifyEvent : RawEvent
    {
        public override string Type => "identify";

        public JsonObject Traits { get; set; }

        internal IdentifyEvent(string userId = null, JsonObject traits = null)
        {
            this.UserId = userId;
            this.Traits = traits;
        }

        internal IdentifyEvent(IdentifyEvent existing) : this(existing.UserId, existing.Traits) => this.ApplyRawEventData(existing);
    }

    public sealed class ScreenEvent : RawEvent
    {
        public override string Type => "screen";

        public string Name { get; set; }

        public string Category { get; set; }

        public JsonObject Properties { get; set; }

        internal ScreenEvent(string category, string title = null, JsonObject properties = null)
        {
            this.Name = title;
            this.Properties = properties;
            this.Category = category;
        }

        internal ScreenEvent(ScreenEvent existing) : this(existing.Category, existing.Name, existing.Properties) => this.ApplyRawEventData(existing);
    }

    public sealed class GroupEvent : RawEvent
    {
        public override string Type => "group";

        public string GroupId { get; set; }

        public JsonObject Traits { get; set; }

        internal GroupEvent(string groupId = null, JsonObject traits = null)
        {
            this.GroupId = groupId;
            this.Traits = traits;
        }

        internal GroupEvent(GroupEvent existing) : this(existing.GroupId, existing.Traits) => this.ApplyRawEventData(existing);
    }

    public sealed class AliasEvent : RawEvent
    {
        public override string Type => "alias";

        public string PreviousId { get; set; }

        internal AliasEvent(string newId, string previousId)
        {
            this.UserId = newId;
            this.PreviousId = previousId;
        }

        internal AliasEvent(AliasEvent existing) : this(existing.UserId, null) => this.ApplyRawEventData(existing);
    }
}
