using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using Segment.Analytics;
using Segment.Serialization;

namespace Segment.Analytics.Compat {
    public class Traits : Dictionary<string, object>
    {
    }

    public class Properties  : Dictionary<string, object>
    {
    }

    public static class AnalyticsExtensions
    {
        public static void Track(this Analytics analytics, string userId, string eventName)
        {
            analytics.Track(eventName, new JsonObject() { {"userId", userId} });
        }
        public static void Track(this Analytics analytics, string userId, string eventName, Dictionary<string, object> properties)
        {
            properties.Add("userId", userId);
            analytics.Track(eventName, new JsonObject(properties.ToDictionary(x=>x.Key, x=>x.Value as JsonElement)));
        }

        public static void Screen(this Analytics analytics, string userId, string eventName)
        {
            analytics.Screen(eventName, new JsonObject() { {"userId", userId} });
        }
        public static void Screen(this Analytics analytics, string userId, string eventName, Dictionary<string, object> properties)
        {
            properties.Add("userId", userId);
            analytics.Screen(eventName, new JsonObject(properties.ToDictionary(x=>x.Key, x=>x.Value as JsonElement)));
        }

        public static void Page(this Analytics analytics, string userId, string eventName)
        {
            analytics.Page(eventName, new JsonObject() { {"userId", userId} });
        }
        public static void Page(this Analytics analytics, string userId, string eventName, Dictionary<string, object> properties)
        {
            properties.Add("userId", userId);
            analytics.Page(eventName, new JsonObject(properties.ToDictionary(x=>x.Key, x=>x.Value as JsonElement)));
        }

        public static void Group(this Analytics analytics, string userId, string groupId, Dictionary<string, object> traits)
        {
            traits.Add("userId", userId);
            analytics.Group(groupId, new JsonObject(traits.ToDictionary(x=>x.Key, x=>x.Value as JsonElement)));
        }

        public static void Alias(this Analytics analytics, string previousId, string userId)
        {
            analytics._userInfo._userId = previousId;
            analytics.Alias(userId);
        }
    }   

    class UserIdPlugin : EventPlugin 
    {
        public override PluginType Type => PluginType.Enrichment;

        public override TrackEvent Track(TrackEvent trackEvent)
        {
            if (trackEvent.Properties.ContainsKey("userId"))
            {
                trackEvent.UserId = trackEvent.Properties.GetString("userId");
                trackEvent.Properties.Remove("userId");
            }
            return trackEvent;
        }

        public override ScreenEvent Screen(ScreenEvent screenEvent)
        {
            if (screenEvent.Properties.ContainsKey("userId"))
            {
                screenEvent.UserId = screenEvent.Properties.GetString("userId");
                screenEvent.Properties.Remove("userId");
            }
            return screenEvent;
        }

        public override PageEvent Page(PageEvent pageEvent)
        {
            if (pageEvent.Properties.ContainsKey("userId"))
            {
                pageEvent.UserId = pageEvent.Properties.GetString("user_id");
                pageEvent.Properties.Remove("userId");
            }
            return pageEvent;
        }

        public override GroupEvent Group(GroupEvent groupEvent)
        {
            if (groupEvent.Traits.ContainsKey("userId"))
            {
                groupEvent.UserId = groupEvent.Traits.GetString("user_id");
                groupEvent.Traits.Remove("userId");
            }
            return groupEvent;
        }
    }
}