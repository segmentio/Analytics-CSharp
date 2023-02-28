namespace Segment.Analytics
{
    using global::System;
    using global::System.Runtime.Serialization;
    using global::System.Threading.Tasks;
    using Segment.Analytics.Plugins;
    using Segment.Analytics.Utilities;
    using Segment.Concurrent;
    using Segment.Serialization;
    using Segment.Sovran;
    using JsonUtility = Serialization.JsonUtility;

    public partial class Analytics : ISubscriber
    {
        public Timeline Timeline { get; }

        internal Configuration Configuration { get; }
        internal Store Store { get; }
        internal IStorage Storage { get; }

        internal Scope AnalyticsScope { get; }
        internal IDispatcher FileIODispatcher { get; }
        internal IDispatcher NetworkIODispatcher { get; }
        internal IDispatcher AnalyticsDispatcher { get; }

        internal static ILogger s_logger = null;

        internal UserInfo _userInfo;

        /// <summary>
        /// Public constructor of Analytics.
        /// </summary>
        /// <param name="configuration">configuration that analytics can use</param>
        public Analytics(Configuration configuration)
        {
            this.Configuration = configuration;
            this.AnalyticsScope = new Scope(configuration.ExceptionHandler);
            if (configuration.UserSynchronizeDispatcher)
            {
                IDispatcher dispatcher = new SynchronizeDispatcher();
                this.FileIODispatcher = dispatcher;
                this.NetworkIODispatcher = dispatcher;
                this.AnalyticsDispatcher = dispatcher;
            }
            else
            {
                this.FileIODispatcher = new Dispatcher(new LimitedConcurrencyLevelTaskScheduler(2));
                this.NetworkIODispatcher = new Dispatcher(new LimitedConcurrencyLevelTaskScheduler(1));
                this.AnalyticsDispatcher = new Dispatcher(new LimitedConcurrencyLevelTaskScheduler(Environment.ProcessorCount));
            }

            this.Store = new Store(configuration.UserSynchronizeDispatcher, configuration.ExceptionHandler);
            this.Storage = configuration.StorageProvider.CreateStorage(this);
            this.Timeline = new Timeline();

            // Start everything
            this.Startup();
        }

        /// <summary>
        /// Process a raw event through the system. Useful when one needs to queue and replay events at a later time.
        /// </summary>
        /// <param name="incomingEvent">An event conforming to RawEvent to be processed in the timeline</param>
        public void Process(RawEvent incomingEvent)
        {
            incomingEvent.ApplyRawEventData();
            _ = this.Timeline.Process(incomingEvent);
        }

        #region System Modifiers

        /// <summary>
        /// Retrieve the anonymousId in a blocking way.
        /// 
        /// Note: this method forces internal async methods to run in a synchronized way,
        /// it's not recommended to be used in async method.
        /// </summary>
        /// <returns>Anonymous Id</returns>
        public string AnonymousId() => this._userInfo._anonymousId;


        /// <summary>
        /// Retrieve the userId registered by a previous <see cref="Identify(string,JsonObject)"/> call in a blocking way.
        /// 
        /// Note: this method forces internal async methods to run in a synchronized way,
        /// it's not recommended to be used in async method.
        /// </summary>
        /// <returns>User Id</returns>
        public string UserId() => this._userInfo._userId;


        /// <summary>
        /// Retrieve the traits registered by a previous <see cref="Identify(string,JsonObject)"/> call in a blocking way.
        /// 
        /// Note: this method forces internal async methods to run in a synchronized way,
        /// it's not recommended to be used in async method.
        /// </summary>
        /// <returns><see cref="JsonObject"/> instance of Traits</returns>
        public JsonObject Traits() => this._userInfo._traits;


        /// <summary>
        /// Retrieve the traits registered by a previous <see cref="Identify(string,JsonObject)"/> call.
        /// </summary>
        /// <typeparam name="T">Type that implements <see cref="ISerializable"/></typeparam>
        /// <returns>Traits</returns>
        public T Traits<T>() where T : ISerializable => this._userInfo._traits != null ? JsonUtility.FromJson<T>(this._userInfo._traits.ToString()) : default;


        /// <summary>
        /// Force all the <see cref="EventPlugin"/> registered in analytics to flush
        /// </summary>
        public void Flush() => this.Apply(plugin =>
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
            this._userInfo._userId = null;
            this._userInfo._anonymousId = null;
            this._userInfo._traits = null;

            _ = this.AnalyticsScope.Launch(this.AnalyticsDispatcher, async () =>
            {
                await this.Store.Dispatch<UserInfo.ResetAction, UserInfo>(new UserInfo.ResetAction());
                this.Apply(plugin =>
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
            var task = this.SettingsAsync();
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
            IState system = await this.Store.CurrentState<System>();
            if (system is System convertedSystem)
            {
                returnSettings = convertedSystem._settings;
            }

            return returnSettings;
        }

        #endregion



        #region Startup

        private void Startup(HTTPClient httpClient = null)
        {
            _ = this.Add(new StartupQueue());
            _ = this.Add(new ContextPlugin());
            _ = this.Add(new UserInfoPlugin());

            // use Wait() for this coroutine to force completion,
            // since Store must be setup before any event call happened.
            // Note: Task.Wait() forces internal async methods to run in a synchronized way,
            // we should avoid of doing it whenever possible.
            this.AnalyticsScope.Launch(this.AnalyticsDispatcher, async () =>
            {
                // load memory with initial value
                this._userInfo = UserInfo.DefaultState(this.Storage);
                await this.Store.Provide(this._userInfo);
                await this.Store.Provide(System.DefaultState(this.Configuration, this.Storage));
                await this.Storage.Initialize();
            }).Wait();

            // check settings over the network,
            // we don't have to Wait() here, because events are piped in
            // StartupQueue until settings is ready
            _ = this.AnalyticsScope.Launch(this.AnalyticsDispatcher, async () =>
            {
                if (this.Configuration.AutoAddSegmentDestination)
                {
                    _ = this.Add(new SegmentDestination());
                }

                await this.CheckSettings(httpClient);
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
