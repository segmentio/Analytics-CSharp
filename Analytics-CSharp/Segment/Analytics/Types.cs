using global::System;
using Segment.Serialization;

namespace Segment.Analytics
{
    public class DestinationMetadata
    {
        public JsonArray Bundled { get; set; }
        public JsonArray Unbundled { get; set; }
        public JsonArray BundledIds { get; set; }
    }

    public abstract class RawEvent
    {
        public virtual string Type { get; set; }
        public virtual string AnonymousId { get; set; }
        public virtual string MessageId { get; set; }
        public virtual string UserId { get; set; }
        public virtual string Timestamp { get; set; }

        public Func<RawEvent, RawEvent> Enrichment { get; set; }

        // JSON types
        public JsonObject Context { get; set; }
        public JsonObject Integrations { get; set; }

        public JsonArray Metrics { get; set; }

        public DestinationMetadata _metadata { get; set; }

        internal void ApplyRawEventData(RawEvent rawEvent)
        {
            AnonymousId = rawEvent.AnonymousId;
            MessageId = rawEvent.MessageId;
            UserId = rawEvent.UserId;
            Timestamp = rawEvent.Timestamp;
            Context = rawEvent.Context;
            Integrations = rawEvent.Integrations;
        }

        internal void ApplyRawEventData(UserInfo userInfo, Func<RawEvent, RawEvent> enrichment)
        {
            Enrichment = enrichment;
            MessageId = Guid.NewGuid().ToString();
            Context = new JsonObject();
            Timestamp = DateTime.UtcNow.ToString("o"); // iso8601
            Integrations = new JsonObject();

            // attach the latest in-memory copy of anonId and userId if not present
            if (string.IsNullOrEmpty(AnonymousId))
            {
                AnonymousId = userInfo._anonymousId;
            }
            if (string.IsNullOrEmpty(UserId))
            {
                UserId = userInfo._userId;
            }
        }
    }

    public sealed class TrackEvent : RawEvent
    {
        public override string Type => "track";

        public string Event { get; set; }

        public JsonObject Properties { get; set; }

        internal TrackEvent(string trackEvent, JsonObject properties)
        {
            Event = trackEvent;
            Properties = properties;
        }

        internal TrackEvent(TrackEvent existing) : this(existing.Event, existing.Properties) => ApplyRawEventData(existing);
    }

    public sealed class IdentifyEvent : RawEvent
    {
        public override string Type => "identify";

        public JsonObject Traits { get; set; }

        internal IdentifyEvent(string userId = null, JsonObject traits = null)
        {
            UserId = userId;
            Traits = traits;
        }

        internal IdentifyEvent(IdentifyEvent existing) => ApplyRawEventData(existing);
    }

    public sealed class ScreenEvent : RawEvent
    {
        public override string Type => "screen";

        public string Name { get; set; }

        public string Category { get; set; }

        public JsonObject Properties { get; set; }

        internal ScreenEvent(string category, string title = null, JsonObject properties = null)
        {
            Name = title;
            Properties = properties;
            Category = category;
        }

        internal ScreenEvent(ScreenEvent existing) : this(existing.Category, existing.Name, existing.Properties) => ApplyRawEventData(existing);
    }

    public sealed class PageEvent : RawEvent
    {
        public override string Type => "page";

        public string Name { get; set; }

        public string Category { get; set; }

        public JsonObject Properties { get; set; }

        internal PageEvent(string category, string title = null, JsonObject properties = null)
        {
            Name = title;
            Properties = properties;
            Category = category;
        }

        internal PageEvent(PageEvent existing) : this(existing.Category, existing.Name, existing.Properties) => ApplyRawEventData(existing);
    }

    public sealed class GroupEvent : RawEvent
    {
        public override string Type => "group";

        public string GroupId { get; set; }

        public JsonObject Traits { get; set; }

        internal GroupEvent(string groupId = null, JsonObject traits = null)
        {
            GroupId = groupId;
            Traits = traits;
        }

        internal GroupEvent(GroupEvent existing) : this(existing.GroupId, existing.Traits) => ApplyRawEventData(existing);
    }

    public sealed class AliasEvent : RawEvent
    {
        public override string Type => "alias";

        public string PreviousId { get; set; }

        internal AliasEvent(string newId, string previousId)
        {
            UserId = newId;
            PreviousId = previousId;
        }

        internal AliasEvent(AliasEvent existing) => ApplyRawEventData(existing);
    }
}
