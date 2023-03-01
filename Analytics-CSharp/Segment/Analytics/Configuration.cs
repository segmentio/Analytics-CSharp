namespace Segment.Analytics
{
    using Segment.Analytics.Utilities;
    using Segment.Concurrent;

    public class Configuration
    {
        public string WriteKey { get; }


        public int FlushAt { get; }

        public int FlushInterval { get; }

        public bool AutoAddSegmentDestination { get; }

        public string ApiHost { get; }

        public string CdnHost { get; }

        public Settings DefaultSettings { get; }

        public bool UserSynchronizeDispatcher { get; }

        public ICoroutineExceptionHandler ExceptionHandler { get; }

        public IStorageProvider StorageProvider { get; }

        /// <summary>
        /// Configuration that analytics can use
        /// </summary>
        /// <param name="writeKey">the Segment writeKey</param>
        /// <param name="flushAt">count of events at which we flush events, defaults to <c>20</c></param>
        /// <param name="flushInterval">interval in seconds at which we flush events, defaults to <c>30 seconds</c></param>
        /// <param name="defaultSettings">settings object that will be used as fallback in case of network failure, defaults to empty</param>
        /// <param name="autoAddSegmentDestination">automatically add SegmentDestination plugin, defaults to <c>true</c></param>
        /// <param name="userSynchronizeDispatcher">forcing everything to run synchronously, used for unit tests </param>
        /// <param name="apiHost">set a default apiHost to which Segment sends events, defaults to <c>api.segment.io/v1</c></param>
        /// <param name="cdnHost">set a default cdnHost to which Segment fetches settings, defaults to <c>cdn-settings.segment.com/v1</c></param>
        /// <param name="exceptionHandler">set an exception handler to handle errors happened in async methods within the analytics scope</param>
        /// <param name="storageProvider">set a storage provide to tell the analytics where to store your data:
        ///     <list type="bullet">
        ///         <item><description><see cref="InMemoryStorageProvider"/> stores data only in memory and ignores the persistentDataPath</description></item>
        ///         <item><description><see cref="DefaultStorageProvider"/> persists data in local disk. This is used by default</description></item>
        ///     </list>
        ///     defaults to DefaultStorageProvider
        /// </param>
        public Configuration(string writeKey,
            int flushAt = 20,
            int flushInterval = 30,
            Settings defaultSettings = new Settings(),
            bool autoAddSegmentDestination = true,
            bool userSynchronizeDispatcher = false,
            string apiHost = null,
            string cdnHost = null,
            ICoroutineExceptionHandler exceptionHandler = null,
            IStorageProvider storageProvider = default)
        {
            WriteKey = writeKey;
            FlushAt = flushAt;
            FlushInterval = flushInterval;
            DefaultSettings = defaultSettings;
            AutoAddSegmentDestination = autoAddSegmentDestination;
            UserSynchronizeDispatcher = userSynchronizeDispatcher;
            ApiHost = apiHost;
            CdnHost = cdnHost;
            ExceptionHandler = exceptionHandler;
            StorageProvider = storageProvider ?? new DefaultStorageProvider();
        }
    }

}
