using System.Collections.Generic;
using System.Linq;
using Segment.Serialization;

namespace Segment.Analytics.Plugins
{
    public class DestinationMetadataPlugin: Plugin
    {
        public override PluginType Type => PluginType.Enrichment;

        private Settings _settings;

        public override void Update(Settings settings, UpdateType type)
        {
            base.Update(settings, type);
            _settings = settings;
        }

        public override RawEvent Execute(RawEvent incomingEvent)
        {
            IEnumerable<Plugin> enabledDestinations = Analytics.Timeline._plugins[PluginType.Destination]._plugins.Where(o =>
                o is DestinationPlugin plugin && !(plugin is SegmentDestination) && plugin._enabled);
            DestinationMetadata metadata = new DestinationMetadata();

            HashSet<string> bundled = new HashSet<string>(enabledDestinations.Select(o => ((DestinationPlugin)o).Key));
            HashSet<string> unbundled = new HashSet<string>();

            // All active integrations, not in `bundled` are put in `unbundled`
            foreach (string integration in _settings.Integrations.Keys)
            {
                if (integration != "Segment.io" && !bundled.Contains(integration))
                {
                    unbundled.Add(integration);
                }
            }

            // All unbundledIntegrations not in `bundled` are put in `unbundled`
            JsonArray unbundledIntegrations =
                _settings.Integrations?.GetJsonObject("Segment.io")?.GetJsonArray("unbundledIntegrations") ??
                new JsonArray();
            foreach (JsonElement integration in unbundledIntegrations)
            {
                string content = ((JsonPrimitive)integration).Content;
                if (!bundled.Contains(content))
                {
                    unbundled.Add(content);
                }
            }

            incomingEvent._metadata = new DestinationMetadata
            {
                Bundled = CreateJsonArray(bundled),
                Unbundled = CreateJsonArray(unbundled),
                BundledIds = new JsonArray()
            };

            return incomingEvent;
        }

        private JsonArray CreateJsonArray(IEnumerable<string> list)
        {
            var jsonArray = new JsonArray();
            foreach (string value in list)
            {
                jsonArray.Add(value);
            }

            return jsonArray;
        }
    }
}
