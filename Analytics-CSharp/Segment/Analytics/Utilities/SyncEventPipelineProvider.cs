using System.Threading;

namespace Segment.Analytics.Utilities
{
    public class SyncEventPipelineProvider: IEventPipelineProvider
    {
        internal int _flushTimeout = -1;
        internal CancellationToken? _flushCancellationToken = null;

        public SyncEventPipelineProvider(
            int flushTimeout = -1,
            CancellationToken? flushCancellationToken = null)
        {
            _flushTimeout = flushTimeout;
            _flushCancellationToken = flushCancellationToken;
        }

        public IEventPipeline Create(Analytics analytics, string key)
        {
            return new SyncEventPipeline(analytics, key, 
                    analytics.Configuration.WriteKey,
                    analytics.Configuration.FlushPolicies,
                    analytics.Configuration.ApiHost,
                    _flushTimeout,
                    _flushCancellationToken);
        }
    }
}