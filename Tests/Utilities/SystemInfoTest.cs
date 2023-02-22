using Segment.Analytics.Utilities;
using Xunit;

namespace Tests.Utilities
{
    public class SystemInfoTest
    {

        [Fact]
        public void GetTest()
        {
            var sysinfo = SystemInfo.get();

            Assert.NotNull(sysinfo);
            Assert.Contains(".NET", sysinfo);
        }
    }
}