using System;
using Segment.Analytics.Utilities;
using Segment.Concurrent;

namespace Segment.Analytics
{
    public class Configuration
    {
        public string WriteKey { get; }

        public int FlushAt { get; }

        public int FlushInterval { get; }

        public bool AutoAddSegmentDestination { get; }

        public string ApiHost { get; }

        public string CdnHost { get; }

        public Settings DefaultSettings { get; }

        public bool UseSynchronizeDispatcher { get; }

        [Obsolete("Please use AnalyticsErrorHandler instead")]
        public ICoroutineExceptionHandler ExceptionHandler {
            get
            {
                return AnalyticsErrorHandler;
            }
            private set
            {
                AnalyticsErrorHandler = new AnalyticsErrorHandlerAdapter(value);
            }
        }

        public IAnalyticsErrorHandler AnalyticsErrorHandler { get; private set; }

        public IStorageProvider StorageProvider { get; }

        public IHTTPClientProvider HttpClientProvider { get; }

        /// <summary>
        /// Configuration that analytics can use
        /// </summary>
        /// <param name="writeKey">the Segment writeKey</param>
        /// <param name="flushAt">count of events at which we flush events, defaults to <c>20</c></param>
        /// <param name="flushInterval">interval in seconds at which we flush events, defaults to <c>30 seconds</c></param>
        /// <param name="defaultSettings">settings object that will be used as fallback in case of network failure, defaults to empty</param>
        /// <param name="autoAddSegmentDestination">automatically add SegmentDestination plugin, defaults to <c>true</c></param>
        /// <param name="useSynchronizeDispatcher">forcing everything to run synchronously, used for unit tests </param>
        /// <param name="apiHost">set a default apiHost to which Segment sends events, defaults to <c>api.segment.io/v1</c></param>
        /// <param name="cdnHost">set a default cdnHost to which Segment fetches settings, defaults to <c>cdn-settings.segment.com/v1</c></param>
        /// <param name="analyticsErrorHandler">set an error handler to handle errors happened in analytics</param>
        /// <param name="storageProvider">set a storage provider to tell the analytics where to store your data:
        ///     <list type="bullet">
        ///         <item><description><see cref="InMemoryStorageProvider"/> stores data only in memory and ignores the persistentDataPath</description></item>
        ///         <item><description><see cref="DefaultStorageProvider"/> persists data in local disk. This is used by default</description></item>
        ///     </list>
        ///     defaults to DefaultStorageProvider
        /// </param>
        /// <param name="httpClientProvider">set a http client provider for analytics use to do network activities:
        ///     <list type="bullet">
        ///         <item><description><see cref="DefaultHTTPClientProvider"/> uses System.Net.Http for network activities</description></item>
        ///     </list>
        ///     defaults to DefaultHTTPClientProvider
        /// </param>
        public Configuration(string writeKey,
            int flushAt = 20,
            int flushInterval = 30,
            Settings defaultSettings = new Settings(),
            bool autoAddSegmentDestination = true,
            bool useSynchronizeDispatcher = false,
            string apiHost = null,
            string cdnHost = null,
            IAnalyticsErrorHandler analyticsErrorHandler = null,
            IStorageProvider storageProvider = default,
            IHTTPClientProvider httpClientProvider = default)
        {
            WriteKey = writeKey;
            FlushAt = flushAt;
            FlushInterval = flushInterval;
            DefaultSettings = defaultSettings;
            AutoAddSegmentDestination = autoAddSegmentDestination;
            UseSynchronizeDispatcher = useSynchronizeDispatcher;
            ApiHost = apiHost;
            CdnHost = cdnHost;
            AnalyticsErrorHandler = analyticsErrorHandler;
            StorageProvider = storageProvider ?? new DefaultStorageProvider();
            HttpClientProvider = httpClientProvider ?? new DefaultHTTPClientProvider();
        }

        public Configuration(string writeKey,
            int flushAt = 20,
            int flushInterval = 30,
            Settings defaultSettings = new Settings(),
            bool autoAddSegmentDestination = true,
            bool userSynchronizeDispatcher = false,
            string apiHost = null,
            string cdnHost = null,
            ICoroutineExceptionHandler exceptionHandler = null,
            IStorageProvider storageProvider = default,
            IHTTPClientProvider httpClientProvider = default) : this(
                writeKey,
                flushAt,
                flushInterval,
                defaultSettings,
                autoAddSegmentDestination,
                userSynchronizeDispatcher,
                apiHost,
                cdnHost,
                exceptionHandler == null ? null : new AnalyticsErrorHandlerAdapter(exceptionHandler),
                storageProvider,
                httpClientProvider)
        {

        }
    }

}
