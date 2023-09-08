namespace Segment.Analytics.Policies
{
    /// <summary>
    /// Flush policy that dictates flushing events at app startup.
    /// </summary>
    public class StartupFlushPolicy : IFlushPolicy
    {
        private bool _flushed = false;

        public bool ShouldFlush()
        {
            if (!_flushed)
            {
                _flushed = true;
                return true;
            }
            else return false;
        }

        public void Schedule(Analytics analytics) {}

        public void Unschedule() {}

        public void UpdateState(RawEvent @event) {}

        public void Reset() {}
    }
}
