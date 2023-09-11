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
            analytics.Track(eventName, JsonUtility.FromJson<Segment.Serialization.JsonObject>(JsonUtility.ToJson(properties)));
        }

        public static void Screen(this Analytics analytics, string userId, string eventName)
        {
            analytics.Screen(eventName, new JsonObject() { {"userId", userId} });
        }
        public static void Screen(this Analytics analytics, string userId, string eventName, Dictionary<string, object> properties)
        {
            properties.Add("userId", userId);
            analytics.Screen(eventName, JsonUtility.FromJson<Segment.Serialization.JsonObject>(JsonUtility.ToJson(properties)));
        }

        public static void Page(this Analytics analytics, string userId, string eventName)
        {
            analytics.Page(eventName, new JsonObject() { {"userId", userId} });
        }
        public static void Page(this Analytics analytics, string userId, string eventName, Dictionary<string, object> properties)
        {
            properties.Add("userId", userId);
            analytics.Page(eventName, JsonUtility.FromJson<Segment.Serialization.JsonObject>(JsonUtility.ToJson(properties)));
        }

        public static void Group(this Analytics analytics, string userId, string groupId, Dictionary<string, object> traits)
        {
            traits.Add("userId", userId);
            analytics.Group(groupId, JsonUtility.FromJson<Segment.Serialization.JsonObject>(JsonUtility.ToJson(traits)));
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

    public override RawEvent Execute(RawEvent incomingEvent)
    {
        switch (incomingEvent)
        {
            case TrackEvent e:
                PatchUserId(e, e.Properties);
                break;
            case PageEvent e:
                PatchUserId(e, e.Properties);
                break;
            case ScreenEvent e:
                PatchUserId(e, e.Properties);
                break;
            case GroupEvent e:
                PatchUserId(e, e.Traits);
                break;
        }

        return incomingEvent;
    }

    private void PatchUserId(RawEvent @event, JsonObject jsonObject)
    {
        if (jsonObject.ContainsKey("userId"))
        {
            @event.UserId = jsonObject.GetString("userId");
            jsonObject.Remove("userId");
        }
    }
}
}