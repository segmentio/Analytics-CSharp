using global::System.Collections.Concurrent;
using Segment.Analytics.Utilities;
using Segment.Concurrent;
using Segment.Sovran;

namespace Segment.Analytics.Plugins
{
    /// <summary>
    /// Analytics plugin to manage started state of analytics client
    /// All events will be held in an in-memory queue until started state is enabled, and once enabled
    /// events will be replayed into the analytics timeline
    /// </summary>
    public class StartupQueue : Plugin, ISubscriber
    {
        private static readonly int s_maxSize = 1000;
        private readonly AtomicBool _running = new AtomicBool(false);
        private readonly ConcurrentQueue<RawEvent> _queuedEvents = new ConcurrentQueue<RawEvent>();

        public override PluginType Type => PluginType.Before;

        public override void Configure(Analytics analytics)
        {
            base.Configure(analytics);
            analytics.AnalyticsScope.Launch(analytics.AnalyticsDispatcher, async () => await analytics.Store.Subscribe<System>(this, state => RunningUpdate((System)state)));
        }

        public override RawEvent Execute(RawEvent incomingEvent)
        {
            if (!_running.Get() && incomingEvent != null)
            {
                Analytics.Logger.Log(LogLevel.Debug, message: "SegmentStartupQueue queueing event");
                // The timeline hasn't started, we need to start queueing so we don't lose events
                if (_queuedEvents.Count >= s_maxSize)
                {
                    // We've exceeded the max size and need to start dropping events
                    _queuedEvents.TryDequeue(out _);
                }
                _queuedEvents.Enqueue(incomingEvent);
                return null;
            }
            // The timeline has started, just let the event pass on to the next plugin
            return incomingEvent;
        }

        private void RunningUpdate(System state)
        {
            Analytics.Logger.Log(LogLevel.Debug, message: "Analytics starting = " + state._running);
            _running.Set(state._running);
            if (_running.Get())
            {
                ReplayEvents();
            }
        }

        private void ReplayEvents()
        {
            while (!_queuedEvents.IsEmpty)
            {
                if (_queuedEvents.TryDequeue(out RawEvent e))
                {
                    Analytics.Process(e);
                }
            }
        }
    }
}
