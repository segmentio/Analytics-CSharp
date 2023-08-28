using System;

namespace Segment.Analytics.Utilities
{
    public interface ISegmentLogger
    {
        void Log(LogLevel logLevel, Exception exception = null, string message = null);
    }

    public enum LogLevel
    {
        Trace, Debug, Information, Warning, Error, Critical, None
    }

    internal class StubLogger : ISegmentLogger
    {
        public void Log(LogLevel logLevel, Exception exception = null, string message = null) {}
    }
}
