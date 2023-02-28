namespace Segment.Analytics
{
    using global::System.Threading.Tasks;
    using Segment.Analytics.Utilities;
    using Segment.Concurrent;
    using Segment.Serialization;

    public struct Settings
    {
        // public Json integrations;
#pragma warning disable IDE1006 // Naming Styles
        public JsonObject Integrations;
        public JsonObject Plan;
        public JsonObject EdgeFunctions;
#pragma warning restore IDE1006 // Naming Styles
    }

    public partial class Analytics
    {
        internal void Update(Settings settings, UpdateType type) => Timeline.Apply(plugin => plugin.Update(settings, type));

        private async Task CheckSettings(HTTPClient httpClient = null)
        {
            httpClient = httpClient ?? new HTTPClient(Configuration.WriteKey, cdnHost: Configuration.CdnHost);
            var systemState = await Store.CurrentState<System>();
            var hasSettings = systemState._settings.Integrations != null && systemState._settings.Plan != null;
            var updateType = hasSettings ? UpdateType.Refresh : UpdateType.Initial;

            await Store.Dispatch<System.ToggleRunningAction, System>(new System.ToggleRunningAction(false));

            await Scope.WithContext(NetworkIODispatcher, async () =>
            {
                var settings = await httpClient.Settings();

                await Scope.WithContext(AnalyticsDispatcher, async () =>
                {
                    if (settings != null)
                    {
                        await Store.Dispatch<System.UpdateSettingsAction, System>(new System.UpdateSettingsAction(settings.Value));
                        Update(settings.Value, updateType);
                    }
                    await Store.Dispatch<System.ToggleRunningAction, System>(new System.ToggleRunningAction(true));
                });
            });
        }
    }
}
