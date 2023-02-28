/*
 * This file here is only for unit tests, thanks to C#,
 * a language designed with no unit testing in mind, that
 * makes mocking/spying unit tests so hard, and then blames
 * the developers not writing testable code. Creating a bunch
 * of useless interfaces and constructors and abusing the factory
 * pattern just for the purpose of tests is simply a bad idea.
 */
namespace Segment.Analytics
{
    using Segment.Analytics.Utilities;
    using Segment.Concurrent;
    using Segment.Sovran;

    public partial class Analytics
    {
        internal Analytics(Configuration configuration,
            Timeline timeline = null,
            Store store = null,
            IStorage storage = null,
            Scope analyticsScope = null,
            IDispatcher fileIODispatcher = null,
            IDispatcher networkIODispatcher = null,
            IDispatcher analyticsDispatcher = null,
            HTTPClient httpClient = null
            )
        {
            this.Configuration = configuration;
            this.AnalyticsScope = analyticsScope ?? new Scope(configuration.ExceptionHandler);
            IDispatcher dispatcher = new SynchronizeDispatcher();
            this.FileIODispatcher = fileIODispatcher ?? dispatcher;
            this.NetworkIODispatcher = networkIODispatcher ?? dispatcher;
            this.AnalyticsDispatcher = analyticsDispatcher ?? dispatcher;
            this.Store = store ?? new Store(true, configuration.ExceptionHandler);
            this.Storage = storage ?? new DefaultStorageProvider().CreateStorage(this);
            this.Timeline = timeline ?? new Timeline();

            this.Startup(httpClient);
        }
    }
}

namespace Segment.Analytics.Utilities
{
    using Segment.Concurrent;

    internal partial class EventPipeline
    {
        internal EventPipeline(
            Analytics analytics,
            HTTPClient httpClient,
            string logTag,
            string apiKey,
            Channel<string> writeChannel = default,
            Channel<string> uploadChannel = default,
            int flushCount = 20,
            long flushIntervalInMillis = 30_000,
            string apiHost = HTTPClient.DefaultAPIHost)
        {
            this._analytics = analytics;
            this._logTag = logTag;
            this._flushCount = flushCount;
            this._flushIntervalInMillis = flushIntervalInMillis;
            this.ApiHost = apiHost;

            this._writeChannel = writeChannel ?? new Channel<string>();
            this._uploadChannel = uploadChannel ?? new Channel<string>();
            this._eventCount = new AtomicInteger(0);
            this._httpClient = httpClient ?? new HTTPClient(apiKey);
            this._storage = analytics.Storage;
            this.Running = false;
        }
    }
}
