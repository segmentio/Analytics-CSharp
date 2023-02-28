namespace Segment.Analytics
{
    using global::System;
    using Segment.Analytics.Utilities;
    using Segment.Serialization;
    using Segment.Sovran;

    /// <summary>
    /// Stores state related to the analytics system:
    ///     <list type="bullet">
    ///         <item><description>configuration used to initialize the client</description></item>
    ///         <item><description>segment settings as a json map</description></item>
    ///         <item><description>running state indicating the system has received settings</description></item>
    ///     </list>
    /// </summary>
    internal struct System : IState
    {
        internal Configuration _configuration;
        internal Settings _settings;
        internal bool _running;

        private System(Configuration configuration, Settings settings, bool running)
        {
            this._configuration = configuration;
            this._settings = settings;
            this._running = running;
        }

        internal static System DefaultState(Configuration configuration, IStorage storage)
        {
            Settings settings;
            try
            {
                var cache = storage.Read(StorageConstants.Settings) ?? "";
                settings = JsonUtility.FromJson<Settings>(cache);
            }
            catch (Exception e)
            {
                Analytics.s_logger?.LogError(e, "Failed to load settings from storage. Switch to default settings provided through configuration.");
                settings = configuration.DefaultSettings;
            }

            return new System(configuration, settings, false);
        }

        internal struct UpdateSettingsAction : IAction
        {
            private Settings _settings;

            public UpdateSettingsAction(Settings settings) => this._settings = settings;

            public IState Reduce(IState state)
            {
                IState result = null;
                if (state is System systemState)
                {
                    result = new System(systemState._configuration, this._settings, systemState._running);
                }

                return result;
            }
        }

        internal readonly struct ToggleRunningAction : IAction
        {
            private readonly bool _running;

            public ToggleRunningAction(bool running) => this._running = running;

            public IState Reduce(IState state)
            {
                IState result = null;
                if (state is System systemState)
                {
                    result = new System(systemState._configuration, systemState._settings, this._running);
                }

                return result;
            }
        }

        internal readonly struct AddDestinationToSettingsAction : IAction
        {
            private readonly string _key;

            public AddDestinationToSettingsAction(string key) => this._key = key;

            public IState Reduce(IState state)
            {
                IState result = null;

                if (state is System systemState)
                {
                    // Check if the settings have this destination
                    var settings = systemState._settings;
                    var integrations = settings.Integrations;
                    integrations[this._key] = true;
                    settings.Integrations = integrations;

                    result = new System(systemState._configuration, settings, systemState._running);
                }

                return result;
            }
        }
    }

    /// <summary>
    /// Stores state related to the user:
    ///     <list type="bullet">
    ///         <item><description>anonymousId (string)</description></item>
    ///         <item><description>userId (string)</description></item>
    ///         <item><description>traits (<see cref="JsonObject"/>)</description></item>
    ///     </list>
    /// </summary>
    internal struct UserInfo : IState
    {
        internal string _anonymousId;
        internal string _userId;
        internal JsonObject _traits;

        private UserInfo(string anonymousId, string userId, JsonObject traits)
        {
            this._anonymousId = anonymousId;
            this._userId = userId;
            this._traits = traits;
        }

        public bool IsNull =>
            this._anonymousId == default &&
            this._userId == default &&
            this._traits == default;

        internal static UserInfo DefaultState(IStorage storage)
        {
            var userId = storage.Read(StorageConstants.UserId);
            var anonymousId = storage.Read(StorageConstants.AnonymousId) ?? Guid.NewGuid().ToString();
            var traitsStr = storage.Read(StorageConstants.Traits) ?? "{}";

            JsonObject traits;
            try
            {
                traits = JsonUtility.FromJson<JsonObject>(traitsStr);
            }
            catch (Exception e)
            {
                Analytics.s_logger?.LogError(e, "Failed to load cached traits from storage, creating an empty traits");
                traits = new JsonObject();
            }

            return new UserInfo(anonymousId, userId, traits);
        }

        internal struct ResetAction : IAction
        {
            public IState Reduce(IState state)
            {
                IState result = null;

                if (state is UserInfo)
                {
                    result = new UserInfo(Guid.NewGuid().ToString(), null, null);
                }

                return result;
            }
        }

        internal readonly struct SetUserIdAction : IAction
        {
            private readonly string _userId;

            public SetUserIdAction(string userId) => this._userId = userId;

            public IState Reduce(IState state)
            {
                IState result = null;
                if (state is UserInfo userState)
                {
                    result = new UserInfo(userState._anonymousId, this._userId, userState._traits);
                }

                return result;
            }
        }

        internal readonly struct SetTraitsAction : IAction
        {
            private readonly JsonObject _traits;

            public SetTraitsAction(JsonObject traits) => this._traits = traits;

            public IState Reduce(IState state)
            {
                IState result = null;
                if (state is UserInfo userState)
                {
                    result = new UserInfo(userState._anonymousId, userState._userId, this._traits);
                }

                return result;
            }
        }

        internal readonly struct SetUserIdAndTraitsAction : IAction
        {
            private readonly string _userId;
            private readonly JsonObject _traits;

            public SetUserIdAndTraitsAction(string userId, JsonObject traits)
            {
                this._userId = userId;
                this._traits = traits;
            }

            public IState Reduce(IState state)
            {
                IState result = null;
                if (state is UserInfo userState)
                {
                    result = new UserInfo(userState._anonymousId, this._userId, this._traits);
                }

                return result;
            }
        }

        internal readonly struct SetAnonymousIdAction : IAction
        {
            private readonly string _anonymousId;

            public SetAnonymousIdAction(string anonymousId) => this._anonymousId = anonymousId;

            public IState Reduce(IState state)
            {
                IState result = null;
                if (state is UserInfo userState)
                {
                    result = new UserInfo(this._anonymousId, userState._userId, userState._traits);
                }

                return result;
            }
        }
    }
}
