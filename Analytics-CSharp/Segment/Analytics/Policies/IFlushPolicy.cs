namespace Segment.Analytics.Policies
{
    public interface IFlushPolicy
    {
        /// <summary>
        /// Called when the policy becomes active. We should start any periodic flushing
        /// we want here.
        /// </summary>
        /// <param name="analytics"></param>
        void Schedule(Analytics analytics);

        /// <summary>
        /// Called when policy should stop running any scheduled flushes
        /// </summary>
        void Unschedule();

        /// <summary>
        /// Called to check whether or not the events should be flushed.
        /// </summary>
        /// <returns></returns>
        bool ShouldFlush();

        /// <summary>
        /// Called as events are added to the timeline and JSON Stringified.
        /// </summary>
        /// <param name="event"></param>
        void UpdateState(RawEvent @event);

        /// <summary>
        /// Called after the events are flushed.
        /// </summary>
        void Reset();
    }
}
