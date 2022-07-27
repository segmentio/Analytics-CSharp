using System;
using System.Runtime.Serialization;
using Segment.Analytics.Plugins;
using Segment.Concurrent;
using Segment.Serialization;
using Segment.Analytics.Utilities;
using Segment.Sovran;
using JsonUtility = Segment.Serialization.JsonUtility;
using System.Threading.Tasks;

namespace Segment.Analytics
{
    public partial class Analytics : ISubscriber
    {
        public Timeline timeline { get; }

        internal Configuration configuration { get; }
        internal Store store { get;}
        internal Storage storage { get;}

        internal Scope analyticsScope { get;}
        internal IDispatcher fileIODispatcher { get;}
        internal IDispatcher networkIODispatcher { get;}
        internal IDispatcher analyticsDispatcher { get;}


        public Analytics(Configuration configuration)
        {
            this.configuration = configuration;

            analyticsScope = new Scope();
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
            
            store = new Store(configuration.userSynchronizeDispatcher);
            storage = new Storage(store, configuration.writeKey, configuration.persistentDataPath, fileIODispatcher);
            timeline = new Timeline();
            
            // Start everything
            Startup();
        }

        /**
         * <summary>
         * Process a raw event through the system. Useful when one needs to queue and replay events at a later time.
         * </summary>
         * <param name="incomingEvent">An event conforming to RawEvent to be processed in the timeline.</param>
         */
        public void Process(RawEvent incomingEvent)
        {
            analyticsScope.Launch(analyticsDispatcher, async () =>
            {
                await incomingEvent.ApplyRawEventData(store);
                timeline.Process(incomingEvent); 
            });
        }
        
        #region System Modifiers

        public string AnonymousId()
        {
            var task = AnonymousIdAsync();
            task.Wait();
            return task.Result;
        }

        public async Task<string> AnonymousIdAsync()
        {
            var userInfo = await store.CurrentState<UserInfo>();
            return userInfo.anonymousId;
        }

        public string UserId()
        {
            var task = UserIdAsync();
            task.Wait();
            return task.Result;
        }

        public async Task<string> UserIdAsync()
        {
            var userInfo = await store.CurrentState<UserInfo>();
            return userInfo.userId;
        }

        public JsonObject Traits()
        {
            var task = TraitsAsync();
            task.Wait();
            return task.Result;
        }

        public async Task<JsonObject> TraitsAsync()
        {
            var userInfo = await store.CurrentState<UserInfo>();
            return userInfo.traits;
        }

        public async Task<T> TraitsAsync<T>() where T : ISerializable
        {   
            var traits = await TraitsAsync();
            return traits != null ? JsonUtility.FromJson<T>(traits.ToString()) : default;
        }

        public void Flush() => Apply(plugin =>
        {
            if (plugin is EventPlugin eventPlugin)
            {
                eventPlugin.Flush();
            }
        });
        

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

        public string Version()
        {
            return Analytics._Version();
        }

        public static string _Version()
        {
            return Segment.Analytics.Version.__segment_version;
        }

        #endregion

        
        
        #region Settings

        public Settings? Settings()
        {
            var task = SettingsAsync();
            task.Wait();
            return task.Result;
        }

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

        private void Startup()
        {
            Add(new StartupQueue());
            Add(new ContextPlugin());

            analyticsScope.Launch(analyticsDispatcher, async () =>
            {

                await store.Provide(UserInfo.DefaultState(configuration, storage));
                await store.Provide(System.DefaultState(configuration, storage));
                await storage.SubscribeToStore();

                if (configuration.autoAddSegmentDestination)
                {
                    Add(new SegmentDestination());
                }

                await CheckSettings();
                // TODO: Add lifecycle events to call CheckSettings when app is brought to foreground (not launched)
            });
        }
        
        #endregion
        
    }
}
