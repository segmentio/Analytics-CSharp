using global::System.Threading.Tasks;
using Segment.Analytics.Utilities;
using Segment.Concurrent;
using Segment.Serialization;

namespace Segment.Analytics
{
    public struct Settings
    {
        // public Json integrations;
        public JsonObject Integrations { get; set; }
        public JsonObject Plan { get; set; }
        public JsonObject EdgeFunctions { get; set; }
    }

    public partial class Analytics
    {
        internal void Update(Settings settings, UpdateType type) => Timeline.Apply(plugin => plugin.Update(settings, type));

        private async Task CheckSettings()
        {
            HTTPClient httpClient = Configuration.HttpClientProvider.CreateHTTPClient(Configuration.WriteKey, cdnHost: Configuration.CdnHost);
            httpClient.AnalyticsRef = this;
            System systemState = await Store.CurrentState<System>();
            bool hasSettings = systemState._settings.Integrations != null && systemState._settings.Plan != null;
            UpdateType updateType = hasSettings ? UpdateType.Refresh : UpdateType.Initial;

            await Store.Dispatch<System.ToggleRunningAction, System>(new System.ToggleRunningAction(false));
            Settings? settings = null;
            await Scope.WithContext(NetworkIODispatcher, async () =>
            {
                settings = await httpClient.Settings();
            });

            if (settings != null)
            {
                await Store.Dispatch<System.UpdateSettingsAction, System>(new System.UpdateSettingsAction(settings.Value));
            }
            else
            {
                settings = systemState._settings;
            }

            Update(settings.Value, updateType);
            await Store.Dispatch<System.ToggleRunningAction, System>(new System.ToggleRunningAction(true));
        }
    }
}
