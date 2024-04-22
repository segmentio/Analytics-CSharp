using System.Collections.Generic;
using System.Threading;
using global::System;
using global::System.Runtime.Serialization;
using global::System.Threading.Tasks;
using Segment.Analytics.Plugins;
using Segment.Analytics.Policies;
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

        internal virtual Scope AnalyticsScope { get; }
        internal virtual IDispatcher FileIODispatcher { get; }
        internal virtual IDispatcher NetworkIODispatcher { get; }
        internal virtual IDispatcher AnalyticsDispatcher { get; }

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
        public virtual string AnonymousId() => _userInfo._anonymousId;


        /// <summary>
        /// Retrieve the userId registered by a previous <see cref="Identify(string,JsonObject)"/> call
        /// </summary>
        /// <returns>User Id</returns>
        public virtual string UserId() => _userInfo._userId;


        /// <summary>
        /// Retrieve the traits registered by a previous <see cref="Identify(string,JsonObject)"/> call
        /// </summary>
        /// <returns><see cref="JsonObject"/>Instance of Traits</returns>
        public virtual JsonObject Traits() => _userInfo._traits;


        /// <summary>
        /// Retrieve the traits registered by a previous <see cref="Identify(string,JsonObject)"/> call.
        /// </summary>
        /// <typeparam name="T">Type that implements <see cref="ISerializable"/></typeparam>
        /// <returns>Traits</returns>
        public virtual T Traits<T>() where T : ISerializable => _userInfo._traits != null ? JsonUtility.FromJson<T>(_userInfo._traits.ToString()) : default;


        /// <summary>
        /// Force all the <see cref="EventPlugin"/> registered in analytics to flush
        /// </summary>
        public virtual void Flush() => Apply(plugin =>
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
        public virtual void Reset()
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
        public virtual Settings Settings()
        {
            Task<Settings> task = SettingsAsync();
            task.Wait();
            return task.Result;
        }

        /// <summary>
        /// Retrieve the settings.
        /// </summary>
        /// <returns>Instance of <see cref="Settings"/></returns>
        public virtual async Task<Settings> SettingsAsync()
        {
            System system = await Store.CurrentState<System>();
            return system._settings;
        }

        #endregion



        #region Startup

        private void Startup()
        {
            Add(new StartupQueue());
            Add(new ContextPlugin());

            // use semaphore for this coroutine to force completion,
            // since Store must be setup before any event call happened.
            SemaphoreSlim semaphore = new SemaphoreSlim(0);
            AnalyticsScope.Launch(AnalyticsDispatcher, async () =>
            {
                try
                {
                    // load memory with initial value
                    _userInfo = UserInfo.DefaultState(Storage);
                    await Store.Provide(_userInfo);
                    await Store.Provide(System.DefaultState(Configuration, Storage));
                    await Storage.Initialize();
                }
                catch (Exception e)
                {
                    ReportInternalError(AnalyticsErrorType.StorageUnknown, e, message: "Unknown Error when restoring settings from storage");
                }
                finally
                {
                    semaphore.Release();
                }
            });
            semaphore.Wait();

            // check settings over the network
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

        #region Flush Policy

        public void RemoveFlushPolicy(params IFlushPolicy[] policies)
        {
            foreach (IFlushPolicy policy in policies)
            {
                policy.Unschedule();
                Configuration.FlushPolicies.Remove(policy);
            }
        }

        public void ClearFlushPolicies()
        {
            foreach (IFlushPolicy policy in Configuration.FlushPolicies)
            {
                policy.Unschedule();
            }
            Configuration.FlushPolicies.Clear();
        }

        public void AddFlushPolicy(params IFlushPolicy[] policies)
        {
            foreach (IFlushPolicy policy in policies)
            {
                Configuration.FlushPolicies.Add(policy);
                if (_enable)
                {
                    policy.Schedule(this);
                }
            }
        }

        #endregion
    }
}
