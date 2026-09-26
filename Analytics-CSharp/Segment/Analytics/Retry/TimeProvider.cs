using System;

namespace Segment.Analytics.Retry
{
    internal interface ITimeProvider
    {
        long CurrentTimeMillis();
    }

    internal class SystemTimeProvider : ITimeProvider
    {
        public long CurrentTimeMillis() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
