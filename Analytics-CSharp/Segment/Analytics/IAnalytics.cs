using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Segment.Analytics.Policies;
using Segment.Serialization;

namespace Segment.Analytics
{
    public interface IAnalytics
    {
        Timeline Timeline { get; }
        bool Enable { get; set; }

        /// <summary>
        /// Retrieve the version of this library in use.
        /// </summary>
        /// <returns>A string representing the version in "BREAKING.FEATURE.FIX" format.</returns>
        string Version { get; }

        /// <summary>
        /// Process a raw event through the system. Useful when one needs to queue and replay events at a later time.
        /// </summary>
        /// <param name="incomingEvent">An event conforming to RawEvent to be processed in the timeline</param>
        void Process(RawEvent incomingEvent);

        /// <summary>
        /// Retrieve the anonymousId.
        /// </summary>
        /// <returns>Anonymous Id</returns>
        string AnonymousId();

        /// <summary>
        /// Retrieve the userId registered by a previous <see cref="Analytics.Identify(string,Segment.Serialization.JsonObject)"/> call
        /// </summary>
        /// <returns>User Id</returns>
        string UserId();

        /// <summary>
        /// Retrieve the traits registered by a previous <see cref="Analytics.Identify(string,Segment.Serialization.JsonObject)"/> call
        /// </summary>
        /// <returns><see cref="JsonObject"/>Instance of Traits</returns>
        JsonObject Traits();

        /// <summary>
        /// Retrieve the traits registered by a previous <see cref="Analytics.Identify(string,Segment.Serialization.JsonObject)"/> call.
        /// </summary>
        /// <typeparam name="T">Type that implements <see cref="ISerializable"/></typeparam>
        /// <returns>Traits</returns>
        T Traits<T>() where T : ISerializable;

        /// <summary>
        /// Force all the <see cref="EventPlugin"/> registered in analytics to flush
        /// </summary>
        void Flush();

        /// <summary>
        /// Reset the user identity info and all the event plugins. Should be invoked when
        /// user logs out
        /// </summary>
        void Reset();

        /// <summary>
        /// Retrieve the settings  in a blocking way.
        ///
        /// Note: this method forces internal async methods to run in a synchronized way,
        /// it's not recommended to be used in async method.
        /// </summary>
        /// <returns>Instance of <see cref="Analytics.Settings"/></returns>
        Settings? Settings();

        /// <summary>
        /// Retrieve the settings.
        /// </summary>
        /// <returns>Instance of <see cref="Analytics.Settings"/></returns>
        Task<Settings?> SettingsAsync();

        /// <summary>
        /// Provides a list of finished, but unsent events.
        /// </summary>
        /// <returns>A list of finished, but unsent events</returns>
        IEnumerable<string> PendingUploads();

        /// <summary>
        /// Purge all pending event upload files.
        /// </summary>
        void PurgeStorage();

        /// <summary>
        /// Purge a single event upload file
        /// </summary>
        /// <param name="filePath">Path to the file to be purged</param>
        void PurgeStorage(string filePath);

        void RemoveFlushPolicy(params IFlushPolicy[] policies);
        void ClearFlushPolicies();
        void AddFlushPolicy(params IFlushPolicy[] policies);

        /// <summary>
        /// The track method is how you record any actions your users perform. Each action is known by a
        /// name, like 'Purchased a T-Shirt'. You can also record properties specific to those actions.
        /// For example a 'Purchased a Shirt' event might have properties like revenue or size.
        /// </summary>
        /// <param name="name">Name of the action</param>
        /// <param name="properties">Properties to describe the action</param>
        void Track(string name, JsonObject properties = default);

        /// <summary>
        /// The track method is how you record any actions your users perform. Each action is known by a
        /// name, like 'Purchased a T-Shirt'. You can also record properties specific to those actions.
        /// For example a 'Purchased a Shirt' event might have properties like revenue or size.
        /// </summary>
        /// <param name="name">Name of the action</param>
        /// <param name="properties">Properties to describe the action</param>
        /// <typeparam name="T">Type that implements <see cref="ISerializable"/></typeparam>
        void Track<T>(string name, T properties = default) where T : ISerializable;

        /// <summary>
        /// Identify lets you tie one of your users and their actions to a recognizable {@code userId}.
        /// It also lets you record {@code traits} about the user, like their email, name, account type,
        /// etc.
        ///
        /// Traits and userId will be automatically cached and available on future sessions for the
        /// same user. To update a trait on the server, call identify with the same user id.
        /// You can also use <see cref="Analytics.Identify(Segment.Serialization.JsonObject)"/> for this purpose.
        ///
        /// In the case when user logs out, make sure to call <see cref="Analytics.Reset"/> to clear user's identity
        /// info.
        ///
        /// </summary>
        /// <param name="userId">Unique identifier which you recognize a user by in your own database</param>
        /// <param name="traits">Traits about the user</param>
        void Identify(string userId, JsonObject traits = default);

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
        /// In the case when user logs out, make sure to call <see cref="Analytics.Reset"/> to clear user's identity
        /// info.
        /// </summary>
        /// <param name="traits">Traits about the user</param>
        void Identify(JsonObject traits);

        /// <summary>
        /// Identify lets you tie one of your users and their actions to a recognizable {@code userId}.
        /// It also lets you record {@code traits} about the user, like their email, name, account type,
        /// etc.
        ///
        /// Traits and userId will be automatically cached and available on future sessions for the
        /// same user. To update a trait on the server, call identify with the same user id.
        /// You can also use <see cref="Analytics.Identify(Segment.Serialization.JsonObject)"/> for this purpose.
        ///
        /// In the case when user logs out, make sure to call <see cref="Analytics.Reset"/> to clear user's identity
        /// info.
        ///
        /// </summary>
        /// <param name="userId">Unique identifier which you recognize a user by in your own database</param>
        /// <param name="traits">Traits about the user</param>
        /// <typeparam name="T">Type that implements <see cref="ISerializable"/></typeparam>
        void Identify<T>(string userId, T traits = default) where T : ISerializable;

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
        /// In the case when user logs out, make sure to call <see cref="Analytics.Reset"/> to clear user's identity
        /// info.
        /// </summary>
        /// <param name="traits">Traits about the user</param>
        /// <typeparam name="T">Type that implements <see cref="ISerializable"/></typeparam>
        void Identify<T>(T traits) where T : ISerializable;

        /// <summary>
        /// The screen methods let your record whenever a user sees a screen of your mobile app, and
        /// attach a name, category or properties to the screen. Either category or name must be
        /// provided.
        /// </summary>
        /// <param name="title">A name for the screen</param>
        /// <param name="properties">Properties to add extra information to this call</param>
        /// <param name="category">A category to describe the screen</param>
        void Screen(string title, JsonObject properties = default, string category = "");

        /// <summary>
        /// The screen methods let your record whenever a user sees a screen of your mobile app, and
        /// attach a name, category or properties to the screen. Either category or name must be
        /// provided.
        /// </summary>
        /// <param name="title">A name for the screen</param>
        /// <param name="properties">Properties to add extra information to this call</param>
        /// <param name="category">A category to describe the screen</param>
        /// <typeparam name="T">Type that implements <see cref="ISerializable"/></typeparam>
        void Screen<T>(string title, T properties = default, string category = "") where T : ISerializable;

        /// <summary>
        /// The page methods let your record whenever a user sees a page of your web app, and
        /// attach a name, category or properties to the page. Either category or name must be
        /// provided.
        /// </summary>
        /// <param name="title">A name for the page</param>
        /// <param name="properties">Properties to add extra information to this call</param>
        /// <param name="category">A category to describe the page</param>
        void Page(string title, JsonObject properties = default, string category = "");

        /// <summary>
        /// The page methods let your record whenever a user sees a page of your mobile app, and
        /// attach a name, category or properties to the page. Either category or name must be
        /// provided.
        /// </summary>
        /// <param name="title">A name for the page</param>
        /// <param name="properties">Properties to add extra information to this call</param>
        /// <param name="category">A category to describe the page</param>
        /// <typeparam name="T">Type that implements <see cref="ISerializable"/></typeparam>
        void Page<T>(string title, T properties = default, string category = "") where T : ISerializable;

        /// <summary>
        /// The group method lets you associate a user with a group. It also lets you record custom
        /// traits about the group, like industry or number of employees.
        ///
        /// If you've called <see cref="Analytics.Identify(string,Segment.Serialization.JsonObject)"/> before, this will
        /// automatically remember the userId. If not, it will fall back to use the anonymousId instead.
        /// </summary>
        /// <param name="groupId">Unique identifier which you recognize a group by in your own database</param>
        /// <param name="traits">Traits about the group</param>
        void Group(string groupId, JsonObject traits = default);

        /// <summary>
        /// The group method lets you associate a user with a group. It also lets you record custom
        /// traits about the group, like industry or number of employees.
        ///
        /// If you've called <see cref="Analytics.Identify(string,Segment.Serialization.JsonObject)"/> before, this will
        /// automatically remember the userId. If not, it will fall back to use the anonymousId instead.
        /// </summary>
        /// <param name="groupId">Unique identifier which you recognize a group by in your own database</param>
        /// <param name="traits">Traits about the group</param>
        /// <typeparam name="T">Type that implements <see cref="ISerializable"/></typeparam>
        void Group<T>(string groupId, T traits = default) where T : ISerializable;

        /// <summary>
        /// The alias method is used to merge two user identities, effectively connecting two sets of
        /// user data as one. This is an advanced method, but it is required to manage user identities
        /// successfully in some of our integrations.
        /// <see href="https://segment.com/docs/tracking-api/alias/">Alias Documentation</see>
        /// </summary>
        /// <param name="newId">The new ID you want to alias the existing ID to. The existing ID will be either
        /// the previousId if you have called identify, or the anonymous ID.
        /// </param>
        void Alias(string newId);

        /// <summary>
        /// Apply a closure to all plugins registered to the analytics client. Ideal for invoking
        /// functions for Utility plugins
        /// </summary>
        /// <param name="closure">Closure of what should be applied</param>
        void Apply(Action<Plugin> closure);

        /// <summary>
        /// Register a plugin to the analytics timeline
        /// </summary>
        /// <param name="plugin">Plugin to be added</param>
        /// <returns>Plugin that added to analytics</returns>
        Plugin Add(Plugin plugin);

        /// <summary>
        /// Remove a plugin from the analytics timeline
        /// </summary>
        /// <param name="plugin">the plugin to be removed</param>
        void Remove(Plugin plugin);

        /// <summary>
        /// Retrieve the first match of registered plugin. It finds
        ///      1. the first instance of the given class/interface
        ///      2. or the first instance of subclass of the given class/interface
        /// </summary>
        /// <typeparam name="T">Type that implements <see cref="Plugin"/></typeparam>
        /// <returns>The plugin instance of given type T</returns>
        T Find<T>() where T : Plugin;

        /// <summary>
        /// Retrieve the first match of registered destination plugin by key. It finds
        /// </summary>
        /// <param name="destinationKey">the key of <see cref="DestinationPlugin"/></param>
        /// <returns></returns>
        DestinationPlugin Find(string destinationKey);

        /// <summary>
        /// Retrieve all matches of registered plugins. It finds
        ///      1. all instances of the given class/interface
        ///      2. and all instances of subclass of the given class/interface
        /// </summary>
        /// <typeparam name="T">Type that implements <see cref="Plugin"/></typeparam>
        /// <returns>A collection of plugins of the given type T</returns>
        IEnumerable<T> FindAll<T>() where T : Plugin;

        /// <summary>
        /// Manually enable a destination plugin.  This is useful when a given DestinationPlugin doesn't have any Segment tie-ins at all.
        /// This will allow the destination to be processed in the same way within this library.
        /// </summary>
        /// <param name="plugin">Destination plugin that needs to be enabled</param>
        void ManuallyEnableDestination(DestinationPlugin plugin);
    }
}
