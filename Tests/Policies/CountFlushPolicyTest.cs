using Segment.Analytics;
using Segment.Analytics.Policies;
using Xunit;

namespace Tests.Policies
{
    public class CountFlushPolicyTest
    {
        [Fact]
        public void TestConstructor()
        {
            Assert.Equal(20, new CountFlushPolicy().FlushAt);
            Assert.Equal(30, new CountFlushPolicy(30).FlushAt);

            // ignores for values less than 1
            Assert.Equal(20, new CountFlushPolicy(0).FlushAt);
            Assert.Equal(20, new CountFlushPolicy(-1).FlushAt);
            Assert.Equal(20, new CountFlushPolicy(-2439872).FlushAt);
        }

        [Fact]
        public void TestFlush()
        {
            int flushAt = 10;
            var policy = new CountFlushPolicy(flushAt);

            // Should NOT flush before any events
            Assert.False(policy.ShouldFlush());

            // all the first 9 events should not cause the policy to be flushed
            for (int i = 1; i < flushAt; i++)
            {
                policy.UpdateState(new ScreenEvent("test"));
                Assert.False(policy.ShouldFlush());
            }

            // next event should trigger the flush
            policy.UpdateState(new ScreenEvent("test"));
            Assert.True(policy.ShouldFlush());

            // Even if we somehow go over the flushAt event limit, the policy should still want to flush
            // events
            policy.UpdateState(new ScreenEvent("test"));
            Assert.True(policy.ShouldFlush());

            // Only when we reset the policy will it not want to flush
            policy.Reset();
            Assert.False(policy.ShouldFlush());

            // The policy will then be ready to count another N events
            for (int i = 1; i < flushAt; i++)
            {
                policy.UpdateState(new ScreenEvent("test"));
                Assert.False(policy.ShouldFlush());
            }

            // but once again the next event will trigger a flush request
            policy.UpdateState(new ScreenEvent("test"));
            Assert.True(policy.ShouldFlush());
        }
    }
}
