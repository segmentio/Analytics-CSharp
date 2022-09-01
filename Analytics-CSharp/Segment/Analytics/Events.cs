using System.Runtime.Serialization;
using Segment.Serialization;

namespace Segment.Analytics
{
    public partial class Analytics
    {
        public void Track(string name, JsonObject properties = default)
        {
            if (properties == null)
            {
                properties = new JsonObject();
            }
            
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
            if (traits == null)
            {
                traits = new JsonObject();
            }

            analyticsScope.Launch(analyticsDispatcher, async () =>
            {
                await store.Dispatch<UserInfo.SetUserIdAndTraitsAction, UserInfo>(
                new UserInfo.SetUserIdAndTraitsAction(userId, traits));
                
                // need to process in scope to prevent
                // user id being overwritten when apply event data
                var identifyEvent = new IdentifyEvent(userId, traits);
                Process(identifyEvent);
            });
        }

        public void Identify(JsonObject traits)
        {
            if (traits == null)
            {
                traits = new JsonObject();
            }

            analyticsScope.Launch(analyticsDispatcher, async () =>
            {
                await store.Dispatch<UserInfo.SetTraitsAction, UserInfo>(
                    new UserInfo.SetTraitsAction(traits));

                var identifyEvent = new IdentifyEvent(traits: traits);
                Process(identifyEvent);
            });
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

        public void Identify<T>(T traits) where T : ISerializable
        {
            if (traits == null)
            {
                Identify(new JsonObject());
            }
            else
            {
                var json = JsonUtility.ToJson(traits);
                Identify(JsonUtility.FromJson<JsonObject>(json));
            }
        }

        public void Screen(string title, JsonObject properties = default, string category = "")
        {
            if (properties == null)
            {
                properties = new JsonObject();
            }
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
            if (traits == null)
            {
                traits = new JsonObject();
            }
            var groupEvent = new GroupEvent(groupId, traits);
            Process(groupEvent);
        }
        
        public void Group<T>(string groupId, T traits = default) where T : ISerializable
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
            analyticsScope.Launch(analyticsDispatcher, async () =>
            {
                var currentUserInfo = await store.CurrentState<UserInfo>();
                if (!currentUserInfo.isNull)
                {
                    var aliasEvent = new AliasEvent(newId, currentUserInfo.userId ?? currentUserInfo.anonymousId);
                    await store.Dispatch<UserInfo.SetUserIdAction, UserInfo>(new UserInfo.SetUserIdAction(newId));
                    Process(aliasEvent);
                }
                else
                {
                    Analytics.logger?.LogError("failed to fetch current userinfo state");
                }
            });
        }
    }
}
