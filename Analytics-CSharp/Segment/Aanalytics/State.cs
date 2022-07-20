using System;
using Segment.Sovran;
using Segment.Serialization;
using Segment.Analytics.Utilities;

namespace Segment.Analytics
{
    internal struct System: IState
    {
        Configuration configuration;
        internal Settings settings;
        internal bool running;

        System(Configuration configuration, Settings settings, bool running)
        {
            this.configuration = configuration;
            this.settings = settings;
            this.running = running;
        }
        
        internal static System DefaultState(Configuration configuration, Storage storage)
        {
            Settings settings;
            try
            {
                var cache = storage.Read(Storage.Constants.Settings) ?? "";
                settings = JsonUtility.FromJson<Settings>(cache);
            }
            catch (Exception _)
            {
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

        internal static UserInfo DefaultState(Configuration configuration, Storage storage)
        {
            var userId = storage.Read(Storage.Constants.UserId);
            var anonymousId = storage.Read(Storage.Constants.AnonymousId) ?? Guid.NewGuid().ToString();
            var traitsStr = storage.Read(Storage.Constants.Traits) ?? "{}";

            JsonObject traits;
            try
            {
                traits = JsonUtility.FromJson<JsonObject>(traitsStr);
            }
            catch (Exception _)
            {
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
