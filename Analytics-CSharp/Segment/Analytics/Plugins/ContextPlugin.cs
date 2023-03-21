
using Segment.Analytics.Utilities;
using Segment.Serialization;

namespace Segment.Analytics.Plugins
{
    /// <summary>
    /// Analytics plugin used to populate events with basic context data.
    /// Auto-added to analytics client on construction
    /// </summary>
    public class ContextPlugin : Plugin
    {
        public override PluginType Type => PluginType.Before;

        private JsonObject _library;

        private const string LibraryKey = "library";

        private const string LibraryNameKey = "name";

        private const string LibraryVersionKey = "version";

        private const string OSKey = "os";

        private const string PlatformKey = "platform";

        public override void Configure(Analytics analytics)
        {
            base.Configure(analytics);
            _library = new JsonObject
            {
                [LibraryNameKey] = "Analytics-CSharp",
                [LibraryVersionKey] = Version.SegmentVersion
            };
        }

        private void ApplyContextData(RawEvent @event)
        {
            var context = new JsonObject(@event.Context?.Content)
            {
                [LibraryKey] = _library,
                [OSKey] = SystemInfo.GetOs(),
                [PlatformKey] = SystemInfo.GetPlatform()
            };

            @event.Context = context;
        }

        public override RawEvent Execute(RawEvent incomingEvent)
        {
            ApplyContextData(incomingEvent);
            return base.Execute(incomingEvent);
        }
    }
}
