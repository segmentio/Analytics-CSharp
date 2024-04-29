using System.Collections.Generic;

namespace Segment.Analytics.Utilities
{
    public interface IEventPipelineProvider
    {
        IEventPipeline Create(Analytics analytics, string key);
    }
}