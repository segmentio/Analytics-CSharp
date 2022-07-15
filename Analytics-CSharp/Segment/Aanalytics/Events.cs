using System.Runtime.Serialization;
using Segment.Serialization;

namespace Segment.Analytics
{
    public partial class Analytics
    {
        public void Track(string name, JsonObject properties = default)
        {
            properties ??= new JsonObject();
            
            var trackEvent = new TrackEvent(name, properties);
            Process(trackEvent);
        }

        public void Track<T>(string name, T properties = default) where T : ISerializable
        {
            if (properties == null)
            {
                Track(name);
            }
            else
            {
                var json = JsonUtility.ToJson(properties);
                Track(name, JsonUtility.FromJson<JsonObject>(json));
            }
        }

        public void Identify(string userId, JsonObject traits = default)
        {
            traits ??= new JsonObject();

            store.Dispatch<UserInfo.SetUserIdAndTraitsAction, UserInfo>(
                new UserInfo.SetUserIdAndTraitsAction(userId, traits));
            var identifyEvent = new IdentifyEvent(userId, traits);
            Process(identifyEvent);
        }
        
        
        public void Identify<T>(string userId, T traits = default) where T : ISerializable
        {
            if (traits == null)
            {
                Identify(userId);
            }
            else
            {
                var json = JsonUtility.ToJson(traits);
                Identify(userId, JsonUtility.FromJson<JsonObject>(json));
            }
        }

        public void Screen(string title, JsonObject properties = default, string category = "")
        {
            properties ??= new JsonObject();
            var screenEvent = new ScreenEvent(category, title, properties);
            Process(screenEvent);
        }
        
        public void Screen<T>(string title, T properties = default, string category = "") where T : ISerializable
        {
            if (properties == null)
            {
                Screen(title, category: category);
            }
            else
            {
                var json = JsonUtility.ToJson(properties);
                Screen(title, JsonUtility.FromJson<JsonObject>(json), category);
            }
        }

        public void Group(string groupId, JsonObject traits = default)
        {
            traits ??= new JsonObject();
            var groupEvent = new GroupEvent(groupId, traits);
        }
        
        public void Group<T>(string groupId, JsonObject traits = default) where T : ISerializable
        {
            if (traits == null)
            {
                Group(groupId);
            }
            else
            {
                var json = JsonUtility.ToJson(traits);
                Group(groupId, JsonUtility.FromJson<JsonObject>(json));
            }
        }

        public void Alias(string newId)
        {
            var currentUserInfo = store.CurrentState<UserInfo>();
            if (!currentUserInfo.isNull)
            {
                var aliasEvent = new AliasEvent(newId, currentUserInfo.userId ?? currentUserInfo.anonymousId);
                store.Dispatch<UserInfo.SetUserIdAction, UserInfo>(new UserInfo.SetUserIdAction(newId));
                Process(aliasEvent);
            }
            else
            {
                // TODO: log failed to fetch current userinfo state
            }
        }
    }
}