using System.Collections.Generic;
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
        internal async Task Update(Settings settings) {
            System systemState = await Store.CurrentState<System>();
            HashSet<int> initializedPlugins = new HashSet<int>();
            Timeline.Apply(plugin => {
                UpdateType type = systemState._initializedPlugins.Contains(plugin.GetHashCode()) ? UpdateType.Refresh : UpdateType.Initial;
                plugin.Update(settings, type);
                initializedPlugins.Add(plugin.GetHashCode());
            });
            await Store.Dispatch<System.AddInitializedPluginAction, System>(new System.AddInitializedPluginAction(initializedPlugins));
        }

        internal async Task CheckSettings()
        {
            HTTPClient httpClient = Configuration.HttpClientProvider.CreateHTTPClient(Configuration.WriteKey, cdnHost: Configuration.CdnHost);
            httpClient.AnalyticsRef = this;
            System systemState = await Store.CurrentState<System>();

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

            await Update(settings.Value);
            await Store.Dispatch<System.ToggleRunningAction, System>(new System.ToggleRunningAction(true));
        }
    }
}
