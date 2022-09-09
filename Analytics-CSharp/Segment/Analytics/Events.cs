using System.Runtime.Serialization;
using Segment.Serialization;

namespace Segment.Analytics
{
    public partial class Analytics
    {
        /// <summary>
        /// The track method is how you record any actions your users perform. Each action is known by a
        /// name, like 'Purchased a T-Shirt'. You can also record properties specific to those actions.
        /// For example a 'Purchased a Shirt' event might have properties like revenue or size.
        /// </summary>
        /// <param name="name">Name of the action</param>
        /// <param name="properties">Properties to describe the action</param>
        public void Track(string name, JsonObject properties = default)
        {
            if (properties == null)
            {
                properties = new JsonObject();
            }
            
            var trackEvent = new TrackEvent(name, properties);
            Process(trackEvent);
        }

        /// <summary>
        /// The track method is how you record any actions your users perform. Each action is known by a
        /// name, like 'Purchased a T-Shirt'. You can also record properties specific to those actions.
        /// For example a 'Purchased a Shirt' event might have properties like revenue or size.
        /// </summary>
        /// <param name="name">Name of the action</param>
        /// <param name="properties">Properties to describe the action</param>
        /// <typeparam name="T">Type that implements <see cref="ISerializable"/></typeparam>
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

        /// <summary>
        /// Identify lets you tie one of your users and their actions to a recognizable {@code userId}.
        /// It also lets you record {@code traits} about the user, like their email, name, account type,
        /// etc.
        ///
        /// Traits and userId will be automatically cached and available on future sessions for the
        /// same user. To update a trait on the server, call identify with the same user id.
        /// You can also use <see cref="Identify(Segment.Serialization.JsonObject)"/> for this purpose.
        ///
        /// In the case when user logs out, make sure to call <see cref="Reset"/> to clear user's identity
        /// info.
        /// 
        /// </summary>
        /// <param name="userId">Unique identifier which you recognize a user by in your own database</param>
        /// <param name="traits">Traits about the user</param>
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

        /// <summary>
        /// Identify lets you tie one of your users and their actions to a recognizable {@code userId}.
        /// It also lets you record {@code traits} about the user, like their email, name, account type,
        /// etc.
        ///
        /// Traits and userId will be automatically cached and available on future sessions for the
        /// same user.
        ///
        /// This method is used to update a trait on the server with the same user id.
        /// 
        /// In the case when user logs out, make sure to call <see cref="Reset"/> to clear user's identity
        /// info.
        /// </summary>
        /// <param name="traits">Traits about the user</param>
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
        
        /// <summary>
        /// Identify lets you tie one of your users and their actions to a recognizable {@code userId}.
        /// It also lets you record {@code traits} about the user, like their email, name, account type,
        /// etc.
        ///
        /// Traits and userId will be automatically cached and available on future sessions for the
        /// same user. To update a trait on the server, call identify with the same user id.
        /// You can also use <see cref="Identify(Segment.Serialization.JsonObject)"/> for this purpose.
        ///
        /// In the case when user logs out, make sure to call <see cref="Reset"/> to clear user's identity
        /// info.
        /// 
        /// </summary>
        /// <param name="userId">Unique identifier which you recognize a user by in your own database</param>
        /// <param name="traits">Traits about the user</param>
        /// <typeparam name="T">Type that implements <see cref="ISerializable"/></typeparam>
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

        /// <summary>
        /// Identify lets you tie one of your users and their actions to a recognizable {@code userId}.
        /// It also lets you record {@code traits} about the user, like their email, name, account type,
        /// etc.
        ///
        /// Traits and userId will be automatically cached and available on future sessions for the
        /// same user.
        ///
        /// This method is used to update a trait on the server with the same user id.
        /// 
        /// In the case when user logs out, make sure to call <see cref="Reset"/> to clear user's identity
        /// info.
        /// </summary>
        /// <param name="traits">Traits about the user</param>
        /// <typeparam name="T">Type that implements <see cref="ISerializable"/></typeparam>
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

        /// <summary>
        /// The screen methods let your record whenever a user sees a screen of your mobile app, and
        /// attach a name, category or properties to the screen. Either category or name must be
        /// provided.
        /// </summary>
        /// <param name="title">A name for the screen</param>
        /// <param name="properties">Properties to add extra information to this call</param>
        /// <param name="category">A category to describe the screen</param>
        public void Screen(string title, JsonObject properties = default, string category = "")
        {
            if (properties == null)
            {
                properties = new JsonObject();
            }
            var screenEvent = new ScreenEvent(category, title, properties);
            Process(screenEvent);
        }
        
        /// <summary>
        /// The screen methods let your record whenever a user sees a screen of your mobile app, and
        /// attach a name, category or properties to the screen. Either category or name must be
        /// provided.
        /// </summary>
        /// <param name="title">A name for the screen</param>
        /// <param name="properties">Properties to add extra information to this call</param>
        /// <param name="category">A category to describe the screen</param>
        /// <typeparam name="T">Type that implements <see cref="ISerializable"/></typeparam>
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

        /// <summary>
        /// The group method lets you associate a user with a group. It also lets you record custom
        /// traits about the group, like industry or number of employees.
        ///
        /// If you've called <see cref="Identify(string,Segment.Serialization.JsonObject)"/> before, this will
        /// automatically remember the userId. If not, it will fall back to use the anonymousId instead.
        /// </summary>
        /// <param name="groupId">Unique identifier which you recognize a group by in your own database</param>
        /// <param name="traits">Traits about the group</param>
        public void Group(string groupId, JsonObject traits = default)
        {
            if (traits == null)
            {
                traits = new JsonObject();
            }
            var groupEvent = new GroupEvent(groupId, traits);
            Process(groupEvent);
        }
        
        /// <summary>
        /// The group method lets you associate a user with a group. It also lets you record custom
        /// traits about the group, like industry or number of employees.
        ///
        /// If you've called <see cref="Identify(string,Segment.Serialization.JsonObject)"/> before, this will
        /// automatically remember the userId. If not, it will fall back to use the anonymousId instead.
        /// </summary>
        /// <param name="groupId">Unique identifier which you recognize a group by in your own database</param>
        /// <param name="traits">Traits about the group</param>
        /// <typeparam name="T">Type that implements <see cref="ISerializable"/></typeparam>
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

        /// <summary>
        /// The alias method is used to merge two user identities, effectively connecting two sets of
        /// user data as one. This is an advanced method, but it is required to manage user identities
        /// successfully in some of our integrations.
        /// <see href="https://segment.com/docs/tracking-api/alias/">Alias Documentation</see>
        /// </summary>
        /// <param name="newId">The new ID you want to alias the existing ID to. The existing ID will be either
        /// the previousId if you have called identify, or the anonymous ID.
        /// </param>
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
