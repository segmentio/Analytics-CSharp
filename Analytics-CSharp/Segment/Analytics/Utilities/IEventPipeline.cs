namespace Segment.Analytics.Utilities
{
    /// <summary>
    /// Abstraction over the event pipeline that buffers, batches, and uploads events.
    /// A custom implementation can be supplied via <see cref="Configuration.EventPipelineProvider"/>.
    ///
    /// NOTE: CDN-driven <c>httpConfig</c> (smart-retry enable/disable and tuning) is applied by
    /// <c>SegmentDestination</c> only to the built-in <c>EventPipeline</c> and <c>SyncEventPipeline</c>.
    /// A custom <see cref="IEventPipeline"/> will not receive it automatically — if your pipeline
    /// needs CDN-driven retry configuration, read it yourself from
    /// <c>settings.Integrations.GetJsonObject("Segment.io").GetJsonObject("httpConfig")</c> in your
    /// plugin's <c>Update(Settings, UpdateType)</c>.
    /// </summary>
    public interface IEventPipeline
    {
        bool Running { get; }
        string ApiHost { get; set; }

        void Put(RawEvent @event);
        void Flush();
        void Start();
        void Stop();
    }
}