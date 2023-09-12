using Segment.Analytics;
using Segment.Analytics.Policies;

namespace ConsoleSample
{
    class FlushOnScreenEventsPolicy : IFlushPolicy
    {
        private bool _screenEventsSeen = false;

        public bool ShouldFlush() => _screenEventsSeen;

        public void UpdateState(RawEvent @event)
        {
            if (@event is ScreenEvent)
            {
                _screenEventsSeen = true;
            }
        }

        public void Reset()
        {
            _screenEventsSeen = false;
        }

        public void Schedule(Analytics analytics) {}

        public void Unschedule() {}
    }
}
