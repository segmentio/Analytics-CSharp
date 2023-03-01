using System;
using Segment.Analytics.Utilities;
using Xunit;

namespace Tests.Utilities
{
    public class SystemInfoTest
    {

        [Fact]
        public void GetPlatformTest()
        {
            var sysinfo = SystemInfo.getPlatform();

            Assert.NotNull(sysinfo);
            Assert.Contains(".NET", sysinfo);
        }

        [Fact]
        public void GetOSTest()
        {
            var sysinfo = SystemInfo.getOS();

            Assert.NotNull(sysinfo);
        }

        [Fact]
        public void GetAppFolderTest()
        {
            var path = SystemInfo.getAppFolder();

            Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), path);
        }
    }
}