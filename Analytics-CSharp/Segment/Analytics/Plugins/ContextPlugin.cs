using Segment.Serialization;

namespace Segment.Analytics.Plugins
{
    public class ContextPlugin : Plugin
    {
        internal override PluginType type => PluginType.Before;

        private JsonObject _library;

        private const string LibraryKey = "library";

        private const string LibraryNameKey = "name";

        private const string LibraryVersionKey = "version";

        internal override void Configure(Analytics analytics)
        {
            base.Configure(analytics);
            _library = new JsonObject
            {
                [LibraryNameKey] = "analytics-csharp",
                [LibraryVersionKey] = Version.SegmentVersion
            };
        }

        private void ApplyContextData(RawEvent @event)
        {
            var context = new JsonObject(@event.context?.content)
            {
                [LibraryKey] = _library
            };

            @event.context = context;
        }

        internal override RawEvent Execute(RawEvent incomingEvent)
        {
            ApplyContextData(incomingEvent);
            return base.Execute(incomingEvent);
        }
    }
}