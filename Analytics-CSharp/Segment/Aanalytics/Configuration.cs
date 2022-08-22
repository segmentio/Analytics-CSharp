using System;

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