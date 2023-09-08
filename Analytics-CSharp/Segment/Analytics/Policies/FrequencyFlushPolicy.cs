using System.Threading;
using System.Threading.Tasks;

namespace Segment.Analytics.Policies
{
    public class FrequencyFlushPolicy : IFlushPolicy
    {
        public long FlushIntervalInMills { get; set; }

        private bool _jobStarted = false;

        private CancellationTokenSource _cts = null;


        public FrequencyFlushPolicy(long flushIntervalInMills = 30 * 1000)
        {
            FlushIntervalInMills = flushIntervalInMills;
        }

        public void Schedule(Analytics analytics)
        {
            if (_jobStarted) return;
            _jobStarted = true;
            _cts = new CancellationTokenSource();

            analytics.AnalyticsScope.Launch(analytics.FileIODispatcher, async () =>
            {
                if (FlushIntervalInMills > 0)
                {
                    while (!_cts.IsCancellationRequested)
                    {
                        analytics.Flush();

                        // use delay to do periodical task
                        // this is doable in coroutine, since delay only suspends, allowing thread to
                        // do other work and then come back.
                        await Task.Delay((int)FlushIntervalInMills);
                    }
                }
            });
        }

        public void Unschedule()
        {
            if (!_jobStarted)
            {
                return;
            }

            _jobStarted = false;
            _cts?.Cancel();
        }

        public bool ShouldFlush() => false; // Always return false; Scheduler will call flush.

        public void UpdateState(RawEvent @event) {}

        public void Reset() {}
    }
}
