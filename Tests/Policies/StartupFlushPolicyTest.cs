using Segment.Analytics;
using Segment.Analytics.Policies;
using Xunit;

namespace Tests.Policies
{
    public class StartupFlushPolicyTest
    {
        [Fact]
        public void TestFlush()
        {
            var policy = new StartupFlushPolicy();

            // Should only flush the first time requested!
            Assert.True(policy.ShouldFlush());

            // Should now not flush any more!
            for (int i = 0; i < 10; i++)
            {
                Assert.False(policy.ShouldFlush());
            }

            // even you call reset; the policy will not want to flush.
            policy.Reset();
            Assert.False(policy.ShouldFlush());

            // Adding events has no effect and does not cause the policy to flush
            policy.UpdateState(new ScreenEvent("test"));
            Assert.False(policy.ShouldFlush());
        }
    }
}
