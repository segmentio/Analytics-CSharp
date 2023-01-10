using System;
using System.Runtime.Serialization;
using Segment.Analytics.Plugins;
using Segment.Concurrent;
using Segment.Serialization;
using Segment.Analytics.Utilities;
using Segment.Sovran;

using JsonUtility = Segment.Serialization.JsonUtility;
using System.Threading.Tasks;
using static Segment.Analytics.Utilities.Storage;

namespace Segment.Analytics
{
    public partial class Analytics : ISubscriber
    {
        public Timeline timeline { get; }

        internal Configuration configuration { get; }
        internal Store store { get; }
        internal Storage storage { get; }

        internal Scope analyticsScope { get; }
        internal IDispatcher fileIODispatcher { get; }
        internal IDispatcher networkIODispatcher { get; }
        internal IDispatcher analyticsDispatcher { get; }

        internal static ILogger logger = null;

        internal UserInfo userInfo;

        /// <summary>
        /// Public constructor of Analytics.
        /// </summary>
        /// <param name="configuration">configuration that analytics can use</param>
        public Analytics(Configuration configuration)
        {
            this.configuration = configuration;
            analyticsScope = new Scope(configuration.exceptionHandler);
            if (configuration.userSynchronizeDispatcher)
            {
                IDispatcher dispatcher = new SynchronizeDispatcher();
                fileIODispatcher = dispatcher;
                networkIODispatcher = dispatcher;
                analyticsDispatcher = dispatcher;
            }
            else
            {
                fileIODispatcher = new Dispatcher(new LimitedConcurrencyLevelTaskScheduler(2));
                networkIODispatcher = new Dispatcher(new LimitedConcurrencyLevelTaskScheduler(1));
                analyticsDispatcher = new Dispatcher(new LimitedConcurrencyLevelTaskScheduler(Environment.ProcessorCount));
            }

            store = new Store(configuration.userSynchronizeDispatcher, configuration.exceptionHandler);
            storage = new Storage(store, configuration.writeKey, configuration.persistentDataPath, fileIODispatcher, configuration.exceptionHandler);
            timeline = new Timeline();

            // Start everything
            Startup();
        }

        /// <summary>
        /// Process a raw event through the system. Useful when one needs to queue and replay events at a later time.
        /// </summary>
        /// <param name="incomingEvent">An event conforming to RawEvent to be processed in the timeline</param>
        public void Process(RawEvent incomingEvent)
        {
            incomingEvent.ApplyBaseData();

            analyticsScope.Launch(analyticsDispatcher, async () =>
            {
                timeline.Process(incomingEvent);
            });
        }

        #region System Modifiers

        /// <summary>
        /// Retrieve the anonymousId in a blocking way.
        /// 
        /// Note: this method forces internal async methods to run in a synchronized way,
        /// it's not recommended to be used in async method.
        /// </summary>
        /// <returns>Anonymous Id</returns>
        public string AnonymousId()
        {
            return userInfo.anonymousId;
        }

        /// <summary>
        /// Retrieve the userId registered by a previous <see cref="Identify(string,Segment.Serialization.JsonObject)"/> call in a blocking way.
        /// 
        /// Note: this method forces internal async methods to run in a synchronized way,
        /// it's not recommended to be used in async method.
        /// </summary>
        /// <returns>User Id</returns>
        public string UserId()
        {
            return userInfo.userId;
        }

        /// <summary>
        /// Retrieve the traits registered by a previous <see cref="Identify(string,Segment.Serialization.JsonObject)"/> call in a blocking way.
        /// 
        /// Note: this method forces internal async methods to run in a synchronized way,
        /// it's not recommended to be used in async method.
        /// </summary>
        /// <returns><see cref="JsonObject"/> instance of Traits</returns>
        public JsonObject Traits()
        {
            return userInfo.traits;
        }


        /// <summary>
        /// Retrieve the traits registered by a previous <see cref="Identify(string,Segment.Serialization.JsonObject)"/> call.
        /// </summary>
        /// <typeparam name="T">Type that implements <see cref="ISerializable"/></typeparam>
        /// <returns>Traits</returns>
        public T Traits<T>() where T : ISerializable
        {
            return userInfo.traits != null ? JsonUtility.FromJson<T>(userInfo.traits.ToString()) : default;
        }


        /// <summary>
        /// Force all the <see cref="EventPlugin"/> registered in analytics to flush
        /// </summary>
        public void Flush() => Apply(plugin =>
        {
            if (plugin is EventPlugin eventPlugin)
            {
                eventPlugin.Flush();
            }
        });


        /// <summary>
        /// Reset the user identity info and all the event plugins. Should be invoked when
        /// user logs out
        /// </summary>
        public void Reset()
        {
            analyticsScope.Launch(analyticsDispatcher, async () =>
            {
                await store.Dispatch<UserInfo.ResetAction, UserInfo>(new UserInfo.ResetAction());
                Apply(plugin =>
                {
                    if (plugin is EventPlugin eventPlugin)
                    {
                        eventPlugin.Reset();
                    }
                });
            });

        }

        /// <summary>
        /// Retrieve the version of this library in use.
        /// </summary>
        /// <returns>A string representing the version in "BREAKING.FEATURE.FIX" format.</returns>
        public string version => Version.SegmentVersion;

        #endregion



        #region Settings

        /// <summary>
        /// Retrieve the settings  in a blocking way.
        /// 
        /// Note: this method forces internal async methods to run in a synchronized way,
        /// it's not recommended to be used in async method.
        /// </summary>
        /// <returns>Instance of <see cref="Settings"/></returns>
        public Settings? Settings()
        {
            var task = SettingsAsync();
            task.Wait();
            return task.Result;
        }

        /// <summary>
        /// Retrieve the settings.
        /// </summary>
        /// <returns>Instance of <see cref="Settings"/></returns>
        public async Task<Settings?> SettingsAsync()
        {
            Settings? returnSettings = null;
            IState system = await store.CurrentState<System>();
            if (system is System convertedSystem)
            {
                returnSettings = convertedSystem.settings;
            }

            return returnSettings;
        }

        #endregion



        #region Startup

        private void Startup(HTTPClient httpClient = null)
        {
            Add(new StartupQueue());
            Add(new ContextPlugin());
            Add(new UserInfoPlugin());

            // use Wait() for this coroutine to force completion,
            // since Store must be setup before any event call happened.
            // Note: Task.Wait() forces internal async methods to run in a synchronized way,
            // we should avoid of doing it whenever possible.
            analyticsScope.Launch(analyticsDispatcher, async () =>
            {
                // load memory with initial value
                userInfo = UserInfo.DefaultState(configuration, storage);
                await store.Provide(UserInfo.DefaultState(configuration, storage));
                await store.Provide(System.DefaultState(configuration, storage));
                await storage.SubscribeToStore();
            }).Wait();

            // check settings over the network,
            // we don't have to Wait() here, because events are piped in
            // StartupQueue until settings is ready
            analyticsScope.Launch(analyticsDispatcher, async () =>
            {
                if (configuration.autoAddSegmentDestination)
                {
                    Add(new SegmentDestination());
                }

                await CheckSettings(httpClient);
                // TODO: Add lifecycle events to call CheckSettings when app is brought to foreground (not launched)
            });
        }

        #endregion

    }

    internal interface ILogger
    {
        void LogError(Exception exception, string message);
        void LogError(string message);
    }
}
