using System;
using System.Runtime.Serialization;
using Segment.Analytics.Plugins;
using Segment.Concurrent;
using Segment.Serialization;
using Segment.Analytics.Utilities;
using Segment.Sovran;
using JsonUtility = Segment.Serialization.JsonUtility;

namespace Segment.Analytics
{
    public partial class Analytics : ISubscriber
    {
        public Timeline timeline { get; }

        internal Configuration configuration { get; }
        internal Store store { get;}
        internal Storage storage { get;}

        internal Scope analyticsScope { get;}
        internal Dispatcher fileIODispatcher { get;}
        internal Dispatcher networkIODispatcher { get;}
        internal Dispatcher analyticsDispatcher { get;}


        public Analytics(Configuration configuration)
        {
            this.configuration = configuration;

            store = new Store();
            storage = new Storage(store, configuration.writeKey, configuration.persistentDataPath);
            timeline = new Timeline();
            
            // Start with default states
            analyticsScope = new Scope();
            fileIODispatcher = new Dispatcher(new LimitedConcurrencyLevelTaskScheduler(2));
            networkIODispatcher = new Dispatcher(new LimitedConcurrencyLevelTaskScheduler(1));
            analyticsDispatcher = new Dispatcher(new LimitedConcurrencyLevelTaskScheduler(Environment.ProcessorCount));

            // TODO: Add the default states
            store.Provide(UserInfo.DefaultState(configuration, storage));
            store.Provide(System.DefaultState(configuration, storage));
            storage.SubscribeToStore();

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
                var updatedEvent = incomingEvent.ApplyRawEventData(store);
                timeline.Process(updatedEvent); 
            });
        }
        
        #region System Modifiers

        string AnonymousId
        {
            get
            {
                UserInfo userInfo = store.CurrentState<UserInfo>();
                return userInfo.anonymousId;
            }
        }
        
        string? UserId
        {
            get
            {
                UserInfo userInfo = store.CurrentState<UserInfo>();
                return userInfo.userId;
            }
        }

        public JsonObject? Traits()
        {
            var userInfo = store.CurrentState<UserInfo>();
            return userInfo.traits;
        }

        public T Traits<T>() where T : ISerializable
        {   
            var traits = Traits();
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
            store.Dispatch<UserInfo.ResetAction, UserInfo>(new UserInfo.ResetAction());
            Apply(plugin =>
            {
                if (plugin is EventPlugin eventPlugin)
                {
                    eventPlugin.Reset();
                }
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
            Settings? returnSettings = null;
            IState system = store.CurrentState<System>();
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
            
            if (configuration.autoAddSegmentDestination)
            {
                Add(new SegmentDestination());
            }

            Add(new ContextPlugin());

            try
            {
                SetupSettingsCheck();
            }
            catch (Exception e)
            {

            }
            
        }
        
        #endregion
        
    }
}
