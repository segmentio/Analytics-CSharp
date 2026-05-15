using System;
using System.Collections.Generic;
using Segment.Analytics.Utilities;
using Segment.Serialization;
using Segment.Sovran;

namespace Segment.Analytics.Plugins
{
    /// <summary>
    /// Segment Analytics plugin that is used to send events to Segment's tracking api, in the choice of region.
    /// How it works:
    /// <list type="number">
    /// <item><description>Plugin receives <c>apiHost</c> settings</description></item>
    /// <item><description>We store events into a file with the batch api format <see href="https://segment.com/docs/connections/sources/catalog/libraries/server/http-api/#batch" /></description></item>
    /// <item><description>We upload events on a dedicated thread using the batch api</description></item>
    /// </list>
    /// </summary>
    public class SegmentDestination : DestinationPlugin, ISubscriber
    {
        private IEventPipeline _pipeline = null;

        public override string Key => "Segment.io";

        internal const string ApiHost = "apiHost";

        public override IdentifyEvent Identify(IdentifyEvent identifyEvent)
        {
            Enqueue(identifyEvent);
            return identifyEvent;
        }

        public override TrackEvent Track(TrackEvent trackEvent)
        {
            Enqueue(trackEvent);
            return trackEvent;
        }

        public override GroupEvent Group(GroupEvent groupEvent)
        {
            Enqueue(groupEvent);
            return groupEvent;
        }

        public override AliasEvent Alias(AliasEvent aliasEvent)
        {
            Enqueue(aliasEvent);
            return aliasEvent;
        }

        public override ScreenEvent Screen(ScreenEvent screenEvent)
        {
            Enqueue(screenEvent);
            return screenEvent;
        }

        public override PageEvent Page(PageEvent pageEvent)
        {
            Enqueue(pageEvent);
            return pageEvent;
        }

        public override void Configure(Analytics analytics)
        {
            base.Configure(analytics);

            // Add DestinationMetadata enrichment plugin
            Add(new DestinationMetadataPlugin());

            _pipeline = analytics.Configuration.EventPipelineProvider.Create(analytics, Key);

            analytics.AnalyticsScope.Launch(analytics.AnalyticsDispatcher, async () =>
            {
                await analytics.Store.Subscribe<System>(this, state => OnEnableToggled((System)state), true);
            });
        }

        public override void Update(Settings settings, UpdateType type)
        {
            base.Update(settings, type);

            JsonObject segmentInfo = settings.Integrations?.GetJsonObject(Key);

            string apiHost = segmentInfo?.GetString(ApiHost);
            if (apiHost != null && _pipeline != null)
                _pipeline.ApiHost = apiHost;

            JsonObject httpConfig = segmentInfo?.GetJsonObject("httpConfig");
            EventPipeline concretePipeline = _pipeline as EventPipeline;
            if (httpConfig != null && concretePipeline?._httpClient != null)
                ApplyHttpConfig(concretePipeline._httpClient, httpConfig);
        }

        private static void ApplyHttpConfig(HTTPClient client, JsonObject httpConfig)
        {
            JsonObject backoff = httpConfig.GetJsonObject("backoffConfig");
            if (backoff != null)
            {
                string enabledStr = backoff.GetString("enabled");
                if (enabledStr != null && bool.TryParse(enabledStr, out bool enabled))
                    client.BackoffEnabled = enabled;

                string maxRetriesStr = backoff.GetString("maxRetryCount");
                if (maxRetriesStr != null && int.TryParse(maxRetriesStr, out int maxRetries))
                    client.MaxRetries = maxRetries;

                string baseStr = backoff.GetString("baseBackoffInterval");
                if (baseStr != null && double.TryParse(baseStr, out double baseMs))
                    client.BaseBackoffMs = baseMs;

                string capStr = backoff.GetString("maxBackoffInterval");
                if (capStr != null && double.TryParse(capStr, out double capMs))
                    client.MaxBackoffMs = capMs;

                JsonObject overridesJson = backoff.GetJsonObject("statusCodeOverrides");
                if (overridesJson != null)
                    client.StatusCodeOverrides = ParseStatusCodeOverrides(overridesJson);
            }

            JsonObject rateLimit = httpConfig.GetJsonObject("rateLimitConfig");
            if (rateLimit != null)
            {
                string enabledStr = rateLimit.GetString("enabled");
                if (enabledStr != null && bool.TryParse(enabledStr, out bool enabled))
                    client.RateLimitEnabled = enabled;

                string maxRetriesStr = rateLimit.GetString("maxRetryCount");
                if (maxRetriesStr != null && int.TryParse(maxRetriesStr, out int maxRetries))
                    client.MaxRateLimitRetries = maxRetries;

                string capStr = rateLimit.GetString("maxRetryInterval");
                if (capStr != null && int.TryParse(capStr, out int capSec))
                    client.MaxRateLimitDuration = TimeSpan.FromSeconds(capSec);
            }
        }

        private static Dictionary<int, string> ParseStatusCodeOverrides(JsonObject overridesJson)
        {
            var result = new Dictionary<int, string>();
            // JsonObject iteration — enumerate known entries via string keys
            // We use a conservative approach: try common status codes
            foreach (int code in new[] { 200, 201, 204, 301, 302, 400, 401, 403, 404, 408, 410, 413,
                                          422, 429, 460, 499, 500, 501, 502, 503, 504, 505, 508, 511 })
            {
                string val = overridesJson.GetString(code.ToString());
                if (val == "retry" || val == "drop")
                    result[code] = val;
            }
            return result;
        }

        public override void Reset()
        {

        }

        public override void Flush() => _pipeline?.Flush();

        private void Enqueue<T>(T payload) where T : RawEvent
        {
            // TODO: filter out empty userid and traits values
            _pipeline?.Put(payload);
        }

        private void OnEnableToggled(System state)
        {
            if (state._enable)
            {
                _pipeline?.Start();
            }
            else
            {
                _pipeline?.Stop();
            }
        }
    }
}
