namespace Segment.Analytics.Plugins
{
    using global::System.Collections.Concurrent;
    using Segment.Concurrent;
    using Segment.Sovran;

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
            _ = analytics.AnalyticsScope.Launch(analytics.AnalyticsDispatcher, async () => await analytics.Store.Subscribe<System>(this, state => this.RunningUpdate((System)state)));
        }

        public override RawEvent Execute(RawEvent incomingEvent)
        {
            if (!this._running.Get() && incomingEvent != null)
            {
                // The timeline hasn't started, we need to start queueing so we don't lose events
                if (this._queuedEvents.Count >= s_maxSize)
                {
                    // We've exceeded the max size and need to start dropping events
                    _ = this._queuedEvents.TryDequeue(out _);
                }
                this._queuedEvents.Enqueue(incomingEvent);
                return null;
            }
            // The timeline has started, just let the event pass on to the next plugin 
            return incomingEvent;
        }

        private void RunningUpdate(System state)
        {
            this._running.Set(state._running);
            if (this._running.Get())
            {
                this.ReplayEvents();
            }
        }

        private void ReplayEvents()
        {
            while (!this._queuedEvents.IsEmpty)
            {
                if (this._queuedEvents.TryDequeue(out var e))
                {
                    this.Analytics.Process(e);
                }
            }
        }
    }
}
