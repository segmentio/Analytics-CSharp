using System.Collections.Generic;
using Segment.Analytics;
using Segment.Serialization;

namespace AnalyticsNetMigrationHelper {
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
        public static void Track(this Analytics analytics, string userId, string eventName, IDictionary<string, object> traits)
        {
            traits.Add("userId", userId);
            analytics.Track(eventName, JsonUtility.FromJson<Segment.Serialization.JsonObject>(JsonUtility.ToJson(traits)));
        }

        public static void Screen(this Analytics analytics, string userId, string eventName)
        {
            analytics.Screen(eventName, new JsonObject() { {"userId", userId} });
        }
        public static void Screen(this Analytics analytics, string userId, string eventName, IDictionary<string, object> traits)
        {
            traits.Add("userId", userId);
            analytics.Screen(eventName, JsonUtility.FromJson<Segment.Serialization.JsonObject>(JsonUtility.ToJson(traits)));
        }

        public static void Page(this Analytics analytics, string userId, string eventName)
        {
            analytics.Page(eventName, new JsonObject() { {"userId", userId} });
        }
        public static void Page(this Analytics analytics, string userId, string eventName, IDictionary<string, object> traits)
        {
            traits.Add("userId", userId);
            analytics.Page(eventName, JsonUtility.FromJson<Segment.Serialization.JsonObject>(JsonUtility.ToJson(traits)));
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
    }
}