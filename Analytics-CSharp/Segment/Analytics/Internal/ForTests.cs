/*
 * This file here is only for unit tests, thanks to C#,
 * a language designed with no unit testing in mind, that
 * makes mocking/spying unit tests so hard, and then blames
 * the developers not writing testable code. Creating a bunch
 * of useless interfaces and constructors and abusing the factory
 * pattern just for the purpose of tests is simply a bad idea.
 */
using Segment.Analytics.Utilities;
using Segment.Concurrent;
using Segment.Sovran;

namespace Segment.Analytics
{
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
            Configuration = configuration;
            AnalyticsScope = analyticsScope ?? new Scope(configuration.ExceptionHandler);
            IDispatcher dispatcher = new SynchronizeDispatcher();
            FileIODispatcher = fileIODispatcher ?? dispatcher;
            NetworkIODispatcher = networkIODispatcher ?? dispatcher;
            AnalyticsDispatcher = analyticsDispatcher ?? dispatcher;
            Store = store ?? new Store(true, configuration.ExceptionHandler);
            Storage = storage ?? new DefaultStorageProvider().CreateStorage(this);
            Timeline = timeline ?? new Timeline();

            Startup(httpClient);
        }
    }
}

namespace Segment.Analytics.Utilities
{
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
            _analytics = analytics;
            _logTag = logTag;
            _flushCount = flushCount;
            _flushIntervalInMillis = flushIntervalInMillis;
            ApiHost = apiHost;

            _writeChannel = writeChannel ?? new Channel<string>();
            _uploadChannel = uploadChannel ?? new Channel<string>();
            _eventCount = new AtomicInteger(0);
            _httpClient = httpClient ?? new HTTPClient(apiKey);
            _storage = analytics.Storage;
            Running = false;
        }
    }
}
