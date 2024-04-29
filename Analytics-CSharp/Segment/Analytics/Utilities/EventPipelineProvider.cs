namespace Segment.Analytics.Utilities
{
    public class EventPipelineProvider:IEventPipelineProvider
    {
        public EventPipelineProvider()
        {
        }

        public IEventPipeline Create(Analytics analytics, string key)
        {
            return new EventPipeline(analytics, key, 
                    analytics.Configuration.WriteKey,
                    analytics.Configuration.FlushPolicies,
                    analytics.Configuration.ApiHost);
        }
    }
}