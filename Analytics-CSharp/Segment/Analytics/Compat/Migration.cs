using System;
using System.Collections.Generic;
using Segment.Serialization;

namespace Segment.Analytics.Compat
{

    [Obsolete("This should only be used if migrating from Analytics.NET or Analytics.Xamarin")]
    public class Traits : Dictionary<string, object>
    {
    }

    [Obsolete("This should only be used if migrating from Analytics.NET or Analytics.Xamarin")]
    public class Properties : Dictionary<string, object>
    {
    }

    public static class AnalyticsExtensions
    {
        [Obsolete("This should only be used if migrating from Analytics.NET or Analytics.Xamarin")]
        public static void Track(this Analytics analytics, string userId, string eventName)
        {
            analytics.Track(eventName, new JsonObject() {{"userId", userId}});
        }

        [Obsolete("This should only be used if migrating from Analytics.NET or Analytics.Xamarin")]
        public static void Track(this Analytics analytics, string userId, string eventName,
            Dictionary<string, object> properties)
        {
            if (properties == null)
            {
                properties = new Dictionary<string, object>();
            }
            properties.Add("userId", userId);
            analytics.Track(eventName,
                JsonUtility.FromJson<JsonObject>(JsonUtility.ToJson(properties)));
        }

        [Obsolete("This should only be used if migrating from Analytics.NET or Analytics.Xamarin")]
        public static void Screen(this Analytics analytics, string userId, string eventName)
        {
            analytics.Screen(eventName, new JsonObject() {{"userId", userId}});
        }

        [Obsolete("This should only be used if migrating from Analytics.NET or Analytics.Xamarin")]
        public static void Screen(this Analytics analytics, string userId, string title,
            Dictionary<string, object> properties)
        {
            if (properties == null)
            {
                properties = new Dictionary<string, object>();
            }
            properties.Add("userId", userId);
            analytics.Screen(title,
                JsonUtility.FromJson<JsonObject>(JsonUtility.ToJson(properties)));
        }

        [Obsolete("This should only be used if migrating from Analytics.NET or Analytics.Xamarin")]
        public static void Page(this Analytics analytics, string userId, string title)
        {
            analytics.Page(title, new JsonObject() {{"userId", userId}});
        }

        [Obsolete("This should only be used if migrating from Analytics.NET or Analytics.Xamarin")]
        public static void Page(this Analytics analytics, string userId, string title,
            Dictionary<string, object> properties)
        {
            if (properties == null)
            {
                properties = new Dictionary<string, object>();
            }
            properties.Add("userId", userId);
            analytics.Page(title,
                JsonUtility.FromJson<JsonObject>(JsonUtility.ToJson(properties)));
        }

        [Obsolete("This should only be used if migrating from Analytics.NET or Analytics.Xamarin")]
        public static void Group(this Analytics analytics, string userId, string groupId,
            Dictionary<string, object> traits)
        {
            if (traits == null)
            {
                traits = new Dictionary<string, object>();
            }
            traits.Add("userId", userId);
            analytics.Group(groupId,
                JsonUtility.FromJson<JsonObject>(JsonUtility.ToJson(traits)));
        }

        [Obsolete("This should only be used if migrating from Analytics.NET or Analytics.Xamarin")]
        public static void Alias(this Analytics analytics, string previousId, string userId)
        {
            analytics._userInfo._userId = previousId;
            analytics.Alias(userId);
        }
    }

    /// <summary>
    /// Plugin that patches user id on a per event basis.
    /// This plugin helps migration from the old Analytics.NET and Analytics.Xamarin libraries,
    /// since Analytics-CSharp does not support passing user id on every track method.
    /// </summary>
    [Obsolete("This should only be used if migrating from Analytics.NET or Analytics.Xamarin")]
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
