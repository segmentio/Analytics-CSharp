using System;
using Segment.Analytics;
using Segment.Analytics.Policies;

namespace ConsoleSample
{
    class NetworkErrorHandler : IAnalyticsErrorHandler
    {
        private Analytics _analytics;

        public NetworkErrorHandler(Analytics analytics)
        {
            _analytics = analytics;
        }

        public void OnExceptionThrown(Exception e)
        {
            if (e is AnalyticsError error && error.ErrorType == AnalyticsErrorType.NetworkServerLimited)
            {
                _analytics.ClearFlushPolicies();
                // Add less persistent flush policies
                _analytics.AddFlushPolicy(new CountFlushPolicy(1000), new FrequencyFlushPolicy(60 * 60 * 1000));
            }
        }
    }
}
