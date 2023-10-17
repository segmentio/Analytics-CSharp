using global::System.Runtime.Serialization;
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
        public virtual void Track(string name, JsonObject properties = default)
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
        public virtual void Track<T>(string name, T properties = default) where T : ISerializable
        {
            if (properties == null)
            {
                Track(name);
            }
            else
            {
                string json = JsonUtility.ToJson(properties);
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
        /// You can also use <see cref="Identify(JsonObject)"/> for this purpose.
        ///
        /// In the case when user logs out, make sure to call <see cref="Reset"/> to clear user's identity
        /// info.
        ///
        /// </summary>
        /// <param name="userId">Unique identifier which you recognize a user by in your own database</param>
        /// <param name="traits">Traits about the user</param>
        public virtual void Identify(string userId, JsonObject traits = default)
        {
            if (traits == null)
            {
                traits = new JsonObject();
            }

            // update cache and persist copy
            _userInfo._userId = userId;
            _userInfo._traits = traits;
            AnalyticsScope.Launch(AnalyticsDispatcher, async () =>
            {
                await Store.Dispatch<UserInfo.SetUserIdAndTraitsAction, UserInfo>(
                new UserInfo.SetUserIdAndTraitsAction(userId, traits));
            });

            var identifyEvent = new IdentifyEvent(userId, traits);
            Process(identifyEvent);
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
        public virtual void Identify(JsonObject traits)
        {
            if (traits == null)
            {
                traits = new JsonObject();
            }

            // update cache and persist copy
            _userInfo._traits = traits;
            AnalyticsScope.Launch(AnalyticsDispatcher, async () =>
            {
                await Store.Dispatch<UserInfo.SetTraitsAction, UserInfo>(
                    new UserInfo.SetTraitsAction(traits));
            });

            var identifyEvent = new IdentifyEvent(_userInfo._userId, traits);
            Process(identifyEvent);
        }

        /// <summary>
        /// Identify lets you tie one of your users and their actions to a recognizable {@code userId}.
        /// It also lets you record {@code traits} about the user, like their email, name, account type,
        /// etc.
        ///
        /// Traits and userId will be automatically cached and available on future sessions for the
        /// same user. To update a trait on the server, call identify with the same user id.
        /// You can also use <see cref="Identify(JsonObject)"/> for this purpose.
        ///
        /// In the case when user logs out, make sure to call <see cref="Reset"/> to clear user's identity
        /// info.
        ///
        /// </summary>
        /// <param name="userId">Unique identifier which you recognize a user by in your own database</param>
        /// <param name="traits">Traits about the user</param>
        /// <typeparam name="T">Type that implements <see cref="ISerializable"/></typeparam>
        public virtual void Identify<T>(string userId, T traits = default) where T : ISerializable
        {
            if (traits == null)
            {
                Identify(userId);
            }
            else
            {
                string json = JsonUtility.ToJson(traits);
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
        public virtual void Identify<T>(T traits) where T : ISerializable
        {
            if (traits == null)
            {
                Identify(new JsonObject());
            }
            else
            {
                string json = JsonUtility.ToJson(traits);
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
        public virtual void Screen(string title, JsonObject properties = default, string category = "")
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
        public virtual void Screen<T>(string title, T properties = default, string category = "") where T : ISerializable
        {
            if (properties == null)
            {
                Screen(title, category: category);
            }
            else
            {
                string json = JsonUtility.ToJson(properties);
                Screen(title, JsonUtility.FromJson<JsonObject>(json), category);
            }
        }


        /// <summary>
        /// The page methods let your record whenever a user sees a page of your web app, and
        /// attach a name, category or properties to the page. Either category or name must be
        /// provided.
        /// </summary>
        /// <param name="title">A name for the page</param>
        /// <param name="properties">Properties to add extra information to this call</param>
        /// <param name="category">A category to describe the page</param>
        public virtual void Page(string title, JsonObject properties = default, string category = "")
        {
            if (properties == null)
            {
                properties = new JsonObject();
            }
            var pageEvent = new PageEvent(category, title, properties);
            Process(pageEvent);
        }

        /// <summary>
        /// The page methods let your record whenever a user sees a page of your mobile app, and
        /// attach a name, category or properties to the page. Either category or name must be
        /// provided.
        /// </summary>
        /// <param name="title">A name for the page</param>
        /// <param name="properties">Properties to add extra information to this call</param>
        /// <param name="category">A category to describe the page</param>
        /// <typeparam name="T">Type that implements <see cref="ISerializable"/></typeparam>
        public virtual void Page<T>(string title, T properties = default, string category = "") where T : ISerializable
        {
            if (properties == null)
            {
                Page(title, category: category);
            }
            else
            {
                string json = JsonUtility.ToJson(properties);
                Page(title, JsonUtility.FromJson<JsonObject>(json), category);
            }
        }

        /// <summary>
        /// The group method lets you associate a user with a group. It also lets you record custom
        /// traits about the group, like industry or number of employees.
        ///
        /// If you've called <see cref="Identify(string,JsonObject)"/> before, this will
        /// automatically remember the userId. If not, it will fall back to use the anonymousId instead.
        /// </summary>
        /// <param name="groupId">Unique identifier which you recognize a group by in your own database</param>
        /// <param name="traits">Traits about the group</param>
        public virtual void Group(string groupId, JsonObject traits = default)
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
        /// If you've called <see cref="Identify(string,JsonObject)"/> before, this will
        /// automatically remember the userId. If not, it will fall back to use the anonymousId instead.
        /// </summary>
        /// <param name="groupId">Unique identifier which you recognize a group by in your own database</param>
        /// <param name="traits">Traits about the group</param>
        /// <typeparam name="T">Type that implements <see cref="ISerializable"/></typeparam>
        public virtual void Group<T>(string groupId, T traits = default) where T : ISerializable
        {
            if (traits == null)
            {
                Group(groupId);
            }
            else
            {
                string json = JsonUtility.ToJson(traits);
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
        public virtual void Alias(string newId)
        {
            var aliasEvent = new AliasEvent(newId, _userInfo._userId ?? _userInfo._anonymousId);

            // update cache and persist copy
            _userInfo._userId = newId;
            AnalyticsScope.Launch(AnalyticsDispatcher, async () =>
            {
                await Store.Dispatch<UserInfo.SetUserIdAction, UserInfo>(new UserInfo.SetUserIdAction(newId));
            });

            Process(aliasEvent);
        }
    }
}
