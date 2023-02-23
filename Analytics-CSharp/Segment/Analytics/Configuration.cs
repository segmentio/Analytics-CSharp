
using System;
using System.IO;
using Segment.Analytics.Utilities;
using Segment.Concurrent;

namespace Segment.Analytics
{
    public class Configuration
    {
        public string writeKey { get; }
        
        public string persistentDataPath { get; }
        
        public int flushAt { get; }
        
        public int flushInterval { get; }
        
        public bool autoAddSegmentDestination { get; }
        
        public string apiHost { get; }
        
        public string cdnHost { get; }
        
        public Settings defaultSettings { get; }

        public bool userSynchronizeDispatcher { get; }
        
        public ICoroutineExceptionHandler exceptionHandler { get; }
        
        public IStorageProvider storageProvider { get; }
        
        /// <summary>
        /// Configuration that analytics can use
        /// </summary>
        /// <param name="writeKey">the Segment writeKey</param>
        /// <param name="persistentDataPath"> 
        /// path where analytics stores data when using a storage provider that writes to disk. for example:
        ///     <list type="bullet">
        ///         <item><description>Xamarin: <c>Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)</c></description></item>
        ///         <item><description>Unity: <c>Application.persistentDataPath</c></description></item>
        ///     </list>
        ///     defaults to Local Application Data
        /// </param>
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
        ///     defaults to DefaultStorageProvider on Unity (Mono) and Xamarin, or to InMemoryStorageProvider on .Net Core
        /// </param>
        public Configuration(string writeKey,
            string persistentDataPath = null,
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
            var platform = SystemInfo.getPlatform();

            this.writeKey = writeKey;
            this.persistentDataPath = persistentDataPath ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            this.flushAt = flushAt;
            this.flushInterval = flushInterval;
            this.defaultSettings = defaultSettings;
            this.autoAddSegmentDestination = autoAddSegmentDestination;
            this.userSynchronizeDispatcher = userSynchronizeDispatcher;
            this.apiHost = apiHost;
            this.cdnHost = cdnHost;
            this.exceptionHandler = exceptionHandler;

            if (storageProvider != null)
            {
                this.storageProvider = storageProvider;
            }
            else if (platform.Contains("Mono") || platform.Contains("Xamarin"))
            {
                this.storageProvider = new DefaultStorageProvider();
            }
            else
            {
                this.storageProvider = new InMemoryStorageProvider();
            }
        }
    }

}