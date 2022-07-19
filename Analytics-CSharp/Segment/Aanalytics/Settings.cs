using System.Threading.Tasks;
using Segment.Serialization;
using Segment.Analytics.Utilities;
using Segment.Concurrent;

namespace Segment.Analytics
{
    public struct Settings
    {
        // public Json integrations;
        public JsonObject integrations;
        public JsonObject plan;
        public JsonObject edgeFunctions;
    }
    
    public partial class Analytics
    {
        internal void Update(Settings settings, UpdateType type)
        {
            timeline.Apply(plugin => plugin.Update(settings, type));
        }
        
        private void SetupSettingsCheck()
        {
            analyticsScope.Launch(networkIODispatcher, async () =>
            {
                await CheckSettings();
            });
            
            // TODO: Add lifecycle events to call CheckSettings when app is brought to foreground (not launched)
        }

        private async Task CheckSettings()
        {
            var httpClient = new HTTPClient(this, cdnHost: configuration.cdnHost);
            var systemState = await store.CurrentState<System>();
            var hasSettings = systemState.settings.integrations != null && systemState.settings.plan != null;
            var updateType = hasSettings ? UpdateType.Refresh : UpdateType.Initial;

            await store.Dispatch<System.ToggleRunningAction, System>(new System.ToggleRunningAction(false));

            await Scope.WithContext(networkIODispatcher, async () =>
            {
                var settings = await httpClient.Settings();

                await Scope.WithContext(analyticsDispatcher, async () =>
                {
                    if (settings != null)
                    {
                        await store.Dispatch<System.UpdateSettingsAction, System>(new System.UpdateSettingsAction(settings.Value));
                        Update(settings.Value, updateType);
                    }
                    await store.Dispatch<System.ToggleRunningAction, System>(new System.ToggleRunningAction(true));
                });
            });
        }
    }
}
