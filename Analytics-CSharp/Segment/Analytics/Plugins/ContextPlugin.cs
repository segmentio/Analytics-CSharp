using Segment.Serialization;

namespace Segment.Analytics.Plugins
{
    public class ContextPlugin : Plugin
    {
        public override PluginType type => PluginType.Before;

        private JsonObject _library;

        private const string LibraryKey = "library";

        private const string LibraryNameKey = "name";

        private const string LibraryVersionKey = "version";

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
            var context = new JsonObject(@event.context?.content)
            {
                [LibraryKey] = _library
            };

            @event.context = context;
        }

        public override RawEvent Execute(RawEvent incomingEvent)
        {
            ApplyContextData(incomingEvent);
            return base.Execute(incomingEvent);
        }
    }
}