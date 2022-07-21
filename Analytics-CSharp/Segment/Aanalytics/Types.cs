using System;
using System.Threading.Tasks;
using Segment.Serialization;
using Segment.Sovran;

namespace Segment.Analytics
{
    public abstract class RawEvent
    {
        public virtual string type { get; set; }
        public virtual string anonymousId { get; set; }
        public virtual string messageId { get; set; }
        public virtual string userId { get; set; }
        public virtual string timestamp { get; set; }

        // JSON types
        public JsonObject context { get; set; }
        public JsonObject integrations { get; set; }
        
        public JsonArray metrics { get; set; }

        internal void ApplyRawEventData(RawEvent rawEvent)
        {
            this.anonymousId = rawEvent.anonymousId;
            this.messageId = rawEvent.messageId;
            this.userId = rawEvent.userId;
            this.timestamp = rawEvent.timestamp;
            this.context = rawEvent.context;
            this.integrations = rawEvent.integrations;
        }

        internal async Task ApplyRawEventData(Store store)
        {
            var userInfo = await store.CurrentState<UserInfo>();
            if (userInfo.isNull) return;

            this.anonymousId = userInfo.anonymousId;
            this.userId = userInfo.userId;
            this.integrations = new JsonObject();  
        }

        internal void ApplyBaseData()
        {
            this.messageId = Guid.NewGuid().ToString();
            this.context = new JsonObject();
            this.timestamp = DateTime.UtcNow.ToString("o"); // iso8601
        }
    }

    public sealed class TrackEvent : RawEvent
    {
        public override string type => "track";
        
        public string @event { get; set; }
        
        public JsonObject properties { get; set; }
        
        internal TrackEvent(string trackEvent, JsonObject properties)
        {
            this.@event = trackEvent;
            this.properties = properties;
        }

        internal TrackEvent(TrackEvent existing) : this(existing.@event, existing.properties)
        {
            ApplyRawEventData(existing);
        }
    }

    public sealed class IdentifyEvent : RawEvent
    {
        public override string type => "identify";
        
        public JsonObject traits { get; set; }

        internal IdentifyEvent(string userId = null, JsonObject traits = null)
        {
            this.userId = userId;
            this.traits = traits;
        }

        internal IdentifyEvent(IdentifyEvent existing) : this(existing.userId, existing.traits)
        {
            ApplyRawEventData(existing);
        }
    }
    
    public sealed class ScreenEvent : RawEvent
    {
        public override string type  => "screen"; 
        
        public string name { get; set; }
        
        public string category { get; set; }
        
        public JsonObject properties { get; set; }

        internal ScreenEvent(string category, string title = null, JsonObject properties = null)
        {
            this.name = title;
            this.properties = properties;
            this.category = category;
        }

        internal ScreenEvent(ScreenEvent existing) : this(existing.category, existing.name, existing.properties)
        {
            ApplyRawEventData(existing);
        }
    }
    
    public sealed class GroupEvent : RawEvent
    {
        public override string type => "group";
        
        public string groupId { get; set; }
        
        public JsonObject traits { get; set; }

        internal GroupEvent(string groupId = null, JsonObject traits = null)
        {
            this.groupId = groupId;
            this.traits = traits;
        }

        internal GroupEvent(GroupEvent existing) : this(existing.groupId, existing.traits)
        {
            ApplyRawEventData(existing);
        }
    }
    
    public sealed class AliasEvent : RawEvent
    {
        public override string type => "alias";
        
        public string previousId { get; set; }

        internal AliasEvent(string newId, string previousId)
        {
            this.userId = newId;
            this.previousId = previousId;
        }

        internal AliasEvent(AliasEvent existing) : this(existing.userId, null)
        {
            ApplyRawEventData(existing);
        }
    }
}