﻿using System.Threading.Tasks;
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

        private async Task CheckSettings(HTTPClient httpClient = null)
        {
            httpClient = httpClient ?? new HTTPClient(configuration.writeKey, cdnHost: configuration.cdnHost);
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
