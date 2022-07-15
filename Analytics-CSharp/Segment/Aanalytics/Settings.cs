using System.Threading.Tasks;
using Segment.Serialization;
using Segment.Analytics.Utilities;

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
            var systemState = store.CurrentState<System>();
            var hasSettings = systemState.settings.integrations != null && systemState.settings.plan != null;
            var updateType = hasSettings ? UpdateType.Refresh : UpdateType.Initial;

            store.Dispatch<System.ToggleRunningAction, System>(new System.ToggleRunningAction(false));
            var settings = await httpClient.Settings();
            if (settings != null)
            {
                store.Dispatch<System.UpdateSettingsAction, System>(new System.UpdateSettingsAction(settings.Value));
                Update(settings.Value, updateType);
            }
            store.Dispatch<System.ToggleRunningAction, System>(new System.ToggleRunningAction(true));
        }
    }
}
