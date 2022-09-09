﻿
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
        
        /// <summary>
        /// Configuration that analytics can use
        /// </summary>
        /// <param name="writeKey">the Segment writeKey</param>
        /// <param name="persistentDataPath"> 
        /// path where analytics stores data. for example:
        ///     <list type="bullet">
        ///         <item><description>Xamarin: <c>Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)</c></description></item>
        ///         <item><description>Unity: <c>Application.persistentDataPath</c></description></item>
        ///     </list>
        /// </param>
        /// <param name="flushAt">count of events at which we flush events, defaults to <c>20</c></param>
        /// <param name="flushInterval">interval in seconds at which we flush events, defaults to <c>30 seconds</c></param>
        /// <param name="defaultSettings">settings object that will be used as fallback in case of network failure, defaults to empty</param>
        /// <param name="autoAddSegmentDestination">automatically add SegmentDestination plugin, defaults to <c>true</c></param>
        /// <param name="userSynchronizeDispatcher">forcing everything to run synchronously, used for unit tests </param>
        /// <param name="apiHost">set a default apiHost to which Segment sends events, defaults to <c>api.segment.io/v1</c></param>
        /// <param name="cdnHost">et a default cdnHost to which Segment fetches settings, defaults to <c>cdn-settings.segment.com/v1</c></param>
        public Configuration(string writeKey,
            string persistentDataPath,
            int flushAt = 20,
            int flushInterval = 30,
            Settings defaultSettings = new Settings(),
            bool autoAddSegmentDestination = true,
            bool userSynchronizeDispatcher = false,
            string apiHost = null,
            string cdnHost = null)
        {
            this.writeKey = writeKey;
            this.persistentDataPath = persistentDataPath;
            this.flushAt = flushAt;
            this.flushInterval = flushInterval;
            this.defaultSettings = defaultSettings;
            this.autoAddSegmentDestination = autoAddSegmentDestination;
            this.userSynchronizeDispatcher = userSynchronizeDispatcher;
            this.apiHost = apiHost;
            this.cdnHost = cdnHost;
        }
    }

}