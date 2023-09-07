using global::System;
using Segment.Analytics.Utilities;
using Segment.Serialization;
using Segment.Sovran;

namespace Segment.Analytics
{
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
        internal bool _enable;

        internal System(Configuration configuration, Settings settings, bool running, bool enable)
        {
            _configuration = configuration;
            _settings = settings;
            _running = running;
            _enable = enable;
        }

        internal static System DefaultState(Configuration configuration, IStorage storage)
        {
            Settings settings;
            try
            {
                string cache = storage.Read(StorageConstants.Settings) ?? "";
                settings = JsonUtility.FromJson<Settings>(cache);
            }
            catch (Exception e)
            {
                Analytics.Logger.Log(LogLevel.Error, e, "Failed to load settings from storage. Switch to default settings provided through configuration.");
                settings = configuration.DefaultSettings;
            }

            return new System(configuration, settings, false, true);
        }

        internal struct UpdateSettingsAction : IAction
        {
            private Settings _settings;

            public UpdateSettingsAction(Settings settings) => _settings = settings;

            public IState Reduce(IState state)
            {
                IState result = null;
                if (state is System systemState)
                {
                    result = new System(systemState._configuration, _settings, systemState._running, systemState._enable);
                }

                return result;
            }
        }

        internal readonly struct ToggleRunningAction : IAction
        {
            private readonly bool _running;

            public ToggleRunningAction(bool running) => _running = running;

            public IState Reduce(IState state)
            {
                IState result = null;
                if (state is System systemState)
                {
                    result = new System(systemState._configuration, systemState._settings, _running, systemState._enable);
                }

                return result;
            }
        }

        internal readonly struct AddDestinationToSettingsAction : IAction
        {
            private readonly string _key;

            public AddDestinationToSettingsAction(string key) => _key = key;

            public IState Reduce(IState state)
            {
                IState result = null;

                if (state is System systemState)
                {
                    // Check if the settings have this destination
                    Settings settings = systemState._settings;
                    settings.Integrations[_key] = true;

                    result = new System(systemState._configuration, settings, systemState._running, systemState._enable);
                }

                return result;
            }
        }

        internal readonly struct ToggleEnabledAction : IAction
        {
            private readonly bool _enable;

            public ToggleEnabledAction(bool enable) => _enable = enable;

            public IState Reduce(IState state)
            {
                IState result = null;
                if (state is System systemState)
                {
                    result = new System(systemState._configuration, systemState._settings, systemState._running, _enable);
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
    public struct UserInfo : IState
    {
        internal string _anonymousId;
        internal string _userId;
        internal JsonObject _traits;

        public UserInfo(string anonymousId, string userId, JsonObject traits)
        {
            _anonymousId = anonymousId;
            _userId = userId;
            _traits = traits;
        }

        public bool IsNull =>
            _anonymousId == default &&
            _userId == default &&
            _traits == default;

        internal static UserInfo DefaultState(IStorage storage)
        {
            string userId = storage.Read(StorageConstants.UserId);
            string anonymousId = storage.Read(StorageConstants.AnonymousId) ?? Guid.NewGuid().ToString();
            string traitsStr = storage.Read(StorageConstants.Traits) ?? "{}";

            JsonObject traits;
            try
            {
                traits = JsonUtility.FromJson<JsonObject>(traitsStr);
            }
            catch (Exception e)
            {
                Analytics.Logger.Log(LogLevel.Error, e, "Failed to load cached traits from storage, creating an empty traits");
                traits = new JsonObject();
            }

            return new UserInfo(anonymousId, userId, traits);
        }

        internal struct ResetAction : IAction
        {
            private readonly string _newAnonymousId;

            public ResetAction(string anonymousId = null) => _newAnonymousId = anonymousId;

            public IState Reduce(IState state)
            {
                IState result = null;

                if (state is UserInfo)
                {
                    string anonymousId = _newAnonymousId ?? Guid.NewGuid().ToString();
                    result = new UserInfo(anonymousId, null, null);
                }

                return result;
            }
        }

        internal readonly struct SetUserIdAction : IAction
        {
            private readonly string _userId;

            public SetUserIdAction(string userId) => _userId = userId;

            public IState Reduce(IState state)
            {
                IState result = null;
                if (state is UserInfo userState)
                {
                    result = new UserInfo(userState._anonymousId, _userId, userState._traits);
                }

                return result;
            }
        }

        internal readonly struct SetTraitsAction : IAction
        {
            private readonly JsonObject _traits;

            public SetTraitsAction(JsonObject traits) => _traits = traits;

            public IState Reduce(IState state)
            {
                IState result = null;
                if (state is UserInfo userState)
                {
                    result = new UserInfo(userState._anonymousId, userState._userId, _traits);
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
                _userId = userId;
                _traits = traits;
            }

            public IState Reduce(IState state)
            {
                IState result = null;
                if (state is UserInfo userState)
                {
                    result = new UserInfo(userState._anonymousId, _userId, _traits);
                }

                return result;
            }
        }

        internal readonly struct SetAnonymousIdAction : IAction
        {
            private readonly string _anonymousId;

            public SetAnonymousIdAction(string anonymousId) => _anonymousId = anonymousId;

            public IState Reduce(IState state)
            {
                IState result = null;
                if (state is UserInfo userState)
                {
                    result = new UserInfo(_anonymousId, userState._userId, userState._traits);
                }

                return result;
            }
        }
    }
}
