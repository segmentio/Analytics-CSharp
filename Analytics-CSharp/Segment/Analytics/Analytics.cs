using System.Collections.Generic;
using global::System;
using global::System.Runtime.Serialization;
using global::System.Threading.Tasks;
using Segment.Analytics.Plugins;
using Segment.Analytics.Utilities;
using Segment.Concurrent;
using Segment.Serialization;
using Segment.Sovran;
using JsonUtility = Segment.Serialization.JsonUtility;

namespace Segment.Analytics
{
    public partial class Analytics : ISubscriber
    {
        public Timeline Timeline { get; }

        public bool Enable
        {
            get => _enable;
            set
            {
                _enable = value;
                AnalyticsScope.Launch(AnalyticsDispatcher, async () =>
                {
                    await Store.Dispatch<System.ToggleEnabledAction, System>(
                        new System.ToggleEnabledAction(value));
                });
            }
        }

        internal Configuration Configuration { get; }
        internal Store Store { get; }
        internal IStorage Storage { get; }

        internal Scope AnalyticsScope { get; }
        internal IDispatcher FileIODispatcher { get; }
        internal IDispatcher NetworkIODispatcher { get; }
        internal IDispatcher AnalyticsDispatcher { get; }

        public static ISegmentLogger Logger = new StubLogger();

        internal UserInfo _userInfo;

        private bool _enable;

        /// <summary>
        /// Public constructor of Analytics.
        /// </summary>
        /// <param name="configuration">configuration that analytics can use</param>
        public Analytics(Configuration configuration)
        {
            Configuration = configuration;
            AnalyticsScope = new Scope(configuration.AnalyticsErrorHandler);
            if (configuration.UseSynchronizeDispatcher)
            {
                IDispatcher dispatcher = new SynchronizeDispatcher();
                FileIODispatcher = dispatcher;
                NetworkIODispatcher = dispatcher;
                AnalyticsDispatcher = dispatcher;
            }
            else
            {
                FileIODispatcher = new Dispatcher(new LimitedConcurrencyLevelTaskScheduler(2));
                NetworkIODispatcher = new Dispatcher(new LimitedConcurrencyLevelTaskScheduler(1));
                AnalyticsDispatcher = new Dispatcher(new LimitedConcurrencyLevelTaskScheduler(Environment.ProcessorCount));
            }

            Store = new Store(configuration.UseSynchronizeDispatcher, configuration.AnalyticsErrorHandler);
            Storage = configuration.StorageProvider.CreateStorage(this);
            Timeline = new Timeline();
            Enable = true;

            // Start everything
            Startup();
        }

        /// <summary>
        /// Process a raw event through the system. Useful when one needs to queue and replay events at a later time.
        /// </summary>
        /// <param name="incomingEvent">An event conforming to RawEvent to be processed in the timeline</param>
        public void Process(RawEvent incomingEvent)
        {
            if (!Enable) return;

            incomingEvent.ApplyRawEventData(_userInfo);
            AnalyticsScope.Launch(AnalyticsDispatcher, () =>
            {
                Timeline.Process(incomingEvent);
            });
        }

        #region System Modifiers

        /// <summary>
        /// Retrieve the anonymousId.
        /// </summary>
        /// <returns>Anonymous Id</returns>
        public string AnonymousId() => _userInfo._anonymousId;


        /// <summary>
        /// Retrieve the userId registered by a previous <see cref="Identify(string,JsonObject)"/> call
        /// </summary>
        /// <returns>User Id</returns>
        public string UserId() => _userInfo._userId;


        /// <summary>
        /// Retrieve the traits registered by a previous <see cref="Identify(string,JsonObject)"/> call
        /// </summary>
        /// <returns><see cref="JsonObject"/>Instance of Traits</returns>
        public JsonObject Traits() => _userInfo._traits;


        /// <summary>
        /// Retrieve the traits registered by a previous <see cref="Identify(string,JsonObject)"/> call.
        /// </summary>
        /// <typeparam name="T">Type that implements <see cref="ISerializable"/></typeparam>
        /// <returns>Traits</returns>
        public T Traits<T>() where T : ISerializable => _userInfo._traits != null ? JsonUtility.FromJson<T>(_userInfo._traits.ToString()) : default;


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
            // update cache and persist storage
            string newAnonymousId = Guid.NewGuid().ToString();
            _userInfo = new UserInfo(newAnonymousId, null, null);
            AnalyticsScope.Launch(AnalyticsDispatcher, async () =>
            {
                await Store.Dispatch<UserInfo.ResetAction, UserInfo>(new UserInfo.ResetAction(newAnonymousId));
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
        public string Version => Segment.Analytics.Version.SegmentVersion;

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
            Task<Settings?> task = SettingsAsync();
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
            IState system = await Store.CurrentState<System>();
            if (system is System convertedSystem)
            {
                returnSettings = convertedSystem._settings;
            }

            return returnSettings;
        }

        #endregion



        #region Startup

        private void Startup()
        {
            Add(new StartupQueue());
            Add(new ContextPlugin());

            // use Wait() for this coroutine to force completion,
            // since Store must be setup before any event call happened.
            // Note: Task.Wait() forces internal async methods to run in a synchronized way,
            // we should avoid of doing it whenever possible.
            AnalyticsScope.Launch(AnalyticsDispatcher, async () =>
            {
                // load memory with initial value
                _userInfo = UserInfo.DefaultState(Storage);
                await Store.Provide(_userInfo);
                await Store.Provide(System.DefaultState(Configuration, Storage));
                await Storage.Initialize();
            }).Wait();

            // check settings over the network,
            // we don't have to Wait() here, because events are piped in
            // StartupQueue until settings is ready
            AnalyticsScope.Launch(AnalyticsDispatcher, async () =>
            {
                if (Configuration.AutoAddSegmentDestination)
                {
                    Add(new SegmentDestination());
                }

                await CheckSettings();
                // TODO: Add lifecycle events to call CheckSettings when app is brought to foreground (not launched)
            });
        }

        #endregion

        #region Storage

        /// <summary>
        /// Provides a list of finished, but unsent events.
        /// </summary>
        /// <returns>A list of finished, but unsent events</returns>
        public IEnumerable<string> PendingUploads()
        {
            return Storage.Read(StorageConstants.Events).Split(',');
        }

        /// <summary>
        /// Purge all pending event upload files.
        /// </summary>
        public void PurgeStorage()
        {
            AnalyticsScope.Launch(FileIODispatcher, () =>
            {
                foreach (string file in PendingUploads())
                {
                    try
                    {
                        Storage.RemoveFile(file);
                    }
                    catch (Exception ex)
                    {
                        this.ReportInternalError(AnalyticsErrorType.StorageUnableToRemove, ex);
                    }
                }
            });
        }

        /// <summary>
        /// Purge a single event upload file
        /// </summary>
        /// <param name="filePath">Path to the file to be purged</param>
        public void PurgeStorage(string filePath)
        {
            AnalyticsScope.Launch(FileIODispatcher, () =>
            {
                try
                {
                    Storage.RemoveFile(filePath);
                }
                catch (Exception ex)
                {
                    this.ReportInternalError(AnalyticsErrorType.StorageUnableToRemove, ex);
                }
            });
        }

        #endregion
    }
}
