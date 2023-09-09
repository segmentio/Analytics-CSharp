namespace Segment.Analytics.Policies
{
    /// <summary>
    /// A Count based Flush Policy that instructs the EventPipeline to flush at the
    /// given @param[flushAt]. The default value is 20. @param[flushAt] values should
    /// be >= 1 or they'll get the default value.
    /// </summary>
    public class CountFlushPolicy : IFlushPolicy
    {
        private int _flushAt;

        private int _count = 0;

        public int FlushAt
        {
            get => _flushAt;
            set {
                _flushAt = value >= 1 ? value : 20;
            }
        }

        public CountFlushPolicy(int flushAt = 20)
        {
            FlushAt = flushAt;
        }

        public bool ShouldFlush()
        {
            return _count >= _flushAt;
        }

        public void UpdateState(RawEvent @event)
        {
            _count++;
        }

        public void Reset()
        {
            _count = 0;
        }

        public void Schedule(Analytics analytics) {}

        public void Unschedule() {}
    }
}
