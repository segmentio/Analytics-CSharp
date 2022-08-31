using System.Collections.Generic;
using Segment.Concurrent;
using Segment.Sovran;

namespace Segment.Analytics.Plugins
{
    public class StartupQueue: Plugin, ISubscriber
    {
        private static int maxSize = 1000;
        private readonly AtomicBool _running = new AtomicBool(false);
        private readonly Queue<RawEvent> _queuedEvents = new Queue<RawEvent>();

        public override PluginType type => PluginType.Before;
        
        public override void Configure(Analytics analytics)
        {
            base.Configure(analytics);
            analytics.analyticsScope.Launch(analytics.analyticsDispatcher, async () =>
            {
                await analytics.store.Subscribe<System>(this, state => RunningUpdate((System)state));
            });
        }

        public override RawEvent Execute(RawEvent incomingEvent)
        {
            if (!_running.Get() && incomingEvent != null)
            {
                // The timeline hasn't started, we need to start queueing so we don't lose events
                if (_queuedEvents.Count >= maxSize)
                {
                    // We've exceeded the max size and need to start dropping events
                    _queuedEvents.Dequeue();
                }
                _queuedEvents.Enqueue(incomingEvent);
                return null;
            }
            // The timeline has started, just let the event pass on to the next plugin 
            return incomingEvent;
        }

        private void RunningUpdate(System state)
        {
            _running.Set(state.running);
            if (_running.Get())
            {
                ReplayEvents();
            }
        }

        private void ReplayEvents()
        {
            while (_queuedEvents.Count != 0)
            {
                analytics.Process(_queuedEvents.Dequeue());
            }
        }
    }
}