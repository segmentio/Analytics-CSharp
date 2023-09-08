using System.Threading.Tasks;
using Moq;
using Segment.Analytics;
using Segment.Analytics.Policies;
using Segment.Concurrent;
using Xunit;

namespace Tests.Policies
{
    public class FrequencyFlushPolicyTest
    {
        private Mock<Analytics> _analytics;
        private FrequencyFlushPolicy _policy;

        public FrequencyFlushPolicyTest()
        {
            var config = new Configuration(
                writeKey: "123",
                autoAddSegmentDestination: false
            );

            _policy = new FrequencyFlushPolicy(1000);
            _analytics = new Mock<Analytics>(config) {CallBase = true};
            _analytics.Setup(o => o.AnalyticsScope).Returns(new Scope());
            _analytics.Setup(o => o.FileIODispatcher)
                .Returns(new Dispatcher(new LimitedConcurrencyLevelTaskScheduler(1)));
            _analytics.Setup(o => o.Flush()).Verifiable();
        }

        [Fact]
        public void TestShouldFlush()
        {
            // FrequencyFlushPolicy.ShouldFlush() should always return false
            Assert.False(_policy.ShouldFlush());

            _policy.Schedule(_analytics.Object);
            Assert.False(_policy.ShouldFlush());

            _policy.Unschedule();
            Assert.False(_policy.ShouldFlush());
        }

        [Fact]
        public async void TestFlushAtScheduled()
        {
            _policy.FlushIntervalInMills = 30 * 1000;
            _policy.Schedule(_analytics.Object);
            await Task.Delay(2500);
            _analytics.Verify(o => o.Flush(), Times.Once);
        }

        [Fact]
        public async void TestFlushPeriodically()
        {
            _policy.Schedule(_analytics.Object);
            await Task.Delay(1500);
            _analytics.Verify(o => o.Flush(), Times.Between(1, 2, Range.Inclusive));
        }

        [Fact]
        public async void TestReschedule()
        {
            _policy.Schedule(_analytics.Object);
            await Task.Delay(1500);
            _analytics.Verify(o => o.Flush(), Times.Between(1, 2, Range.Inclusive));

            _policy.Unschedule();
            await Task.Delay(1500);
            // now that it is unscheduled, the count of Flush call should not be increased.
            _analytics.Verify(o => o.Flush(), Times.Between(1, 2, Range.Inclusive));

            _policy.Schedule(_analytics.Object);
            await Task.Delay(1500);
            // now that it is scheduled again, the count of Flush should resume increasing.
            _analytics.Verify(o => o.Flush(), Times.AtLeast(3));
        }
    }
}
