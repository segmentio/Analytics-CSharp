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
            string sysinfo = SystemInfo.GetPlatform();

            Assert.NotNull(sysinfo);
            Assert.Contains(".NET", sysinfo);
        }

        [Fact]
        public void GetOSTest()
        {
            string sysinfo = SystemInfo.GetOs();

            Assert.NotNull(sysinfo);
        }

        [Fact]
        public void GetAppFolderTest()
        {
            string path = SystemInfo.GetAppFolder();

            Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), path);
        }
    }
}
