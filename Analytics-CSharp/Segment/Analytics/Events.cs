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
        /// <param name="properties">Properties to describe the action.
        /// <para>
        /// Analytics internally serializes/deserializes object in the following way:
        /// <list type="bullet">
        /// <item>
        /// <description>Properties: only public properties are serialized. Fields are ignored completely.
        /// To include fields in serialization, check <a href="https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/how-to?pivots=dotnet-8-0#include-fields">here</a>
        /// for more details
        /// </description>
        /// </item>
        /// <item>
        /// <description>Camel case: all properties are serialized and deserialized in camel cases.
        /// To customize property names, check <a href="https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/customize-properties?pivots=dotnet-7-0">here</a>
        /// for more details
        /// </description>
        /// </item>
        /// </list>
        /// </para>
        /// </param>
        public void Track(string name, object properties)
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
        public void Identify(string userId, JsonObject traits = default)
        {
            if (traits == null)
            {
                traits = new JsonObject();
            }

            AnalyticsScope.Launch(AnalyticsDispatcher, async () =>
            {
                await Store.Dispatch<UserInfo.SetUserIdAndTraitsAction, UserInfo>(
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

            AnalyticsScope.Launch(AnalyticsDispatcher, async () =>
            {
                await Store.Dispatch<UserInfo.SetTraitsAction, UserInfo>(
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
        /// You can also use <see cref="Identify(JsonObject)"/> for this purpose.
        ///
        /// In the case when user logs out, make sure to call <see cref="Reset"/> to clear user's identity
        /// info.
        ///
        /// </summary>
        /// <param name="userId">Unique identifier which you recognize a user by in your own database</param>
        /// <param name="traits">Traits about the user.
        /// <para>
        /// Analytics internally serializes/deserializes object in the following way:
        /// <list type="bullet">
        /// <item>
        /// <description>Properties: only public properties are serialized. Fields are ignored completely.
        /// To include fields in serialization, check <a href="https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/how-to?pivots=dotnet-8-0#include-fields">here</a>
        /// for more details
        /// </description>
        /// </item>
        /// <item>
        /// <description>Camel case: all properties are serialized and deserialized in camel cases.
        /// To customize property names, check <a href="https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/customize-properties?pivots=dotnet-7-0">here</a>
        /// for more details
        /// </description>
        /// </item>
        /// </list>
        /// </para>
        /// </param>
        public void Identify(string userId, object traits)
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
        /// <param name="traits">Traits about the user.
        /// <para>
        /// Analytics internally serializes/deserializes object in the following way:
        /// <list type="bullet">
        /// <item>
        /// <description>Properties: only public properties are serialized. Fields are ignored completely.
        /// To include fields in serialization, check <a href="https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/how-to?pivots=dotnet-8-0#include-fields">here</a>
        /// for more details
        /// </description>
        /// </item>
        /// <item>
        /// <description>Camel case: all properties are serialized and deserialized in camel cases.
        /// To customize property names, check <a href="https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/customize-properties?pivots=dotnet-7-0">here</a>
        /// for more details
        /// </description>
        /// </item>
        /// </list>
        /// </para>
        /// </param>
        public void Identify(object traits)
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
        /// <param name="category">A category to describe the screen.
        /// <para>
        /// Analytics internally serializes/deserializes object in the following way:
        /// <list type="bullet">
        /// <item>
        /// <description>Properties: only public properties are serialized. Fields are ignored completely.
        /// To include fields in serialization, check <a href="https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/how-to?pivots=dotnet-8-0#include-fields">here</a>
        /// for more details
        /// </description>
        /// </item>
        /// <item>
        /// <description>Camel case: all properties are serialized and deserialized in camel cases.
        /// To customize property names, check <a href="https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/customize-properties?pivots=dotnet-7-0">here</a>
        /// for more details
        /// </description>
        /// </item>
        /// </list>
        /// </para>
        /// </param>
        public void Screen(string title, object properties, string category = "")
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
        /// The group method lets you associate a user with a group. It also lets you record custom
        /// traits about the group, like industry or number of employees.
        ///
        /// If you've called <see cref="Identify(string,JsonObject)"/> before, this will
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
        /// If you've called <see cref="Identify(string,JsonObject)"/> before, this will
        /// automatically remember the userId. If not, it will fall back to use the anonymousId instead.
        /// </summary>
        /// <param name="groupId">Unique identifier which you recognize a group by in your own database</param>
        /// <param name="traits">Traits about the group.
        /// <para>
        /// Analytics internally serializes/deserializes object in the following way:
        /// <list type="bullet">
        /// <item>
        /// <description>Properties: only public properties are serialized. Fields are ignored completely.
        /// To include fields in serialization, check <a href="https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/how-to?pivots=dotnet-8-0#include-fields">here</a>
        /// for more details
        /// </description>
        /// </item>
        /// <item>
        /// <description>Camel case: all properties are serialized and deserialized in camel cases.
        /// To customize property names, check <a href="https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/customize-properties?pivots=dotnet-7-0">here</a>
        /// for more details
        /// </description>
        /// </item>
        /// </list>
        /// </para>
        /// </param>
        public void Group(string groupId, object traits)
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
        public void Alias(string newId) => AnalyticsScope.Launch(AnalyticsDispatcher, async () =>
                                                    {
                                                        UserInfo currentUserInfo = await Store.CurrentState<UserInfo>();
                                                        if (!currentUserInfo.IsNull)
                                                        {
                                                            var aliasEvent = new AliasEvent(newId, currentUserInfo._userId ?? currentUserInfo._anonymousId);
                                                            await Store.Dispatch<UserInfo.SetUserIdAction, UserInfo>(new UserInfo.SetUserIdAction(newId));
                                                            Process(aliasEvent);
                                                        }
                                                        else
                                                        {
                                                            s_logger?.LogError("failed to fetch current userinfo state");
                                                        }
                                                    });
    }
}
