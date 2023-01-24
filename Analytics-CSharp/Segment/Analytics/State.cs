using System;
using Segment.Sovran;
using Segment.Serialization;
using Segment.Analytics.Utilities;

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
    internal struct System: IState
    {
        internal Configuration configuration;
        internal Settings settings;
        internal bool running;

        System(Configuration configuration, Settings settings, bool running)
        {
            this.configuration = configuration;
            this.settings = settings;
            this.running = running;
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
                Analytics.logger?.LogError(e, "Failed to load settings from storage. Switch to default settings provided through configuration.");
                settings = configuration.defaultSettings;
            }

            return new System(configuration, settings, false);
        }

        internal struct UpdateSettingsAction : IAction
        {
            Settings settings;

            public UpdateSettingsAction(Settings settings)
            {
                this.settings = settings;
            }

            public IState Reduce(IState state)
            {
                IState result = null;
                if (state is System systemState)
                {
                    result = new System(systemState.configuration, settings, systemState.running);                    
                }
                
                return result;
            }
        }

        internal struct ToggleRunningAction : IAction
        {
            private bool running;

            public ToggleRunningAction(bool running)
            {
                this.running = running;
            }

            public IState Reduce(IState state)
            {
                IState result = null;
                if (state is System systemState)
                {
                    result = new System(systemState.configuration, systemState.settings, running);                    
                }
                
                return result;
            }
        }

        internal struct AddDestinationToSettingsAction : IAction
        {
            private string key;

            public AddDestinationToSettingsAction(string key)
            {
                this.key = key;
            }

            public IState Reduce(IState state)
            {
                IState result = null;

                if (state is System systemState)
                {
                    // Check if the settings have this destination
                    Settings settings = systemState.settings;
                    var integrations = settings.integrations;
                    integrations[key] = true;
                    settings.integrations = integrations;
                    
                    result = new System(systemState.configuration, settings,systemState.running);                    
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
    struct UserInfo : IState
    {
        internal string anonymousId;
        internal string userId;
        internal JsonObject traits;

        UserInfo(string anonymousId, string userId, JsonObject traits)
        {
            this.anonymousId = anonymousId;
            this.userId = userId;
            this.traits = traits;
        }

        public bool isNull =>
            anonymousId == default &&
            userId == default &&
            traits == default;

        internal static UserInfo DefaultState(Configuration configuration, IStorage storage)
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
                Analytics.logger?.LogError(e, "Failed to load cached traits from storage, creating an empty traits");
                traits = new JsonObject();
            }
            
            return new UserInfo(anonymousId, userId, traits);
        }

        internal struct ResetAction : IAction
        {
            public IState Reduce(IState state)
            {
                IState result = null;
                if (state is UserInfo userState)
                {
                    result = new UserInfo(Guid.NewGuid().ToString(), null, null);                    
                }
                
                return result;
            }
        }
        
        internal struct SetUserIdAction : IAction
        {
            private string userId;

            public SetUserIdAction(string userId)
            {
                this.userId = userId;
            }

            public IState Reduce(IState state)
            {
                IState result = null;
                if (state is UserInfo userState)
                {
                    result = new UserInfo(userState.anonymousId, userId, userState.traits);                    
                }
                
                return result;
            }
        }

        internal struct SetTraitsAction : IAction
        {
            private JsonObject traits;

            public SetTraitsAction(JsonObject traits)
            {
                this.traits = traits;
            }

            public IState Reduce(IState state)
            {
                IState result = null;
                if (state is UserInfo userState)
                {
                    result = new UserInfo(userState.anonymousId, userState.userId, traits);
                }
                
                return result;
            }
        }
        
        internal struct SetUserIdAndTraitsAction : IAction
        {
            private string userId;
            private JsonObject traits;

            public SetUserIdAndTraitsAction(string userId, JsonObject traits)
            {
                this.userId = userId;
                this.traits = traits;
            }

            public IState Reduce(IState state)
            {
                IState result = null;
                if (state is UserInfo userState)
                {
                    result = new UserInfo(userState.anonymousId, userId, traits);
                }
                
                return result;
            }
        }
        
        internal struct SetAnonymousIdAction : IAction
        {
            private string anonymousId;

            public SetAnonymousIdAction(string anonymousId)
            {
                this.anonymousId = anonymousId;
            }

            public IState Reduce(IState state)
            {
                IState result = null;
                if (state is UserInfo userState)
                {
                    result = new UserInfo(anonymousId, userState.userId, userState.traits);
                }
                
                return result;
            }
        }
    }
}
