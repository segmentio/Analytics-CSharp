using System;
using Segment.Analytics.Utilities;
using Xunit;

namespace Tests.Retry
{
    public class RetryAfterParserTest
    {
        [Fact]
        public void Parse_IntegerSeconds_ReturnsParsedValue()
        {
            Assert.Equal(60, RetryAfterParser.Parse("60"));
        }

        [Fact]
        public void Parse_IntegerWithWhitespace_ReturnsParsedValue()
        {
            Assert.Equal(120, RetryAfterParser.Parse("  120  "));
        }

        [Fact]
        public void Parse_Null_ReturnsNull()
        {
            Assert.Null(RetryAfterParser.Parse(null));
        }

        [Fact]
        public void Parse_Empty_ReturnsNull()
        {
            Assert.Null(RetryAfterParser.Parse(""));
        }

        [Fact]
        public void Parse_HttpDate_InFuture_ReturnsSeconds()
        {
            var now = new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero);
            // 2 seconds in the future
            string httpDate = "Tue, 16 Jun 2026 12:00:02 GMT";

            int? result = RetryAfterParser.Parse(httpDate, now);

            Assert.Equal(2, result);
        }

        [Fact]
        public void Parse_HttpDate_InPast_ReturnsNull()
        {
            var now = new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero);
            // 10 seconds in the past
            string httpDate = "Tue, 16 Jun 2026 11:59:50 GMT";

            int? result = RetryAfterParser.Parse(httpDate, now);

            Assert.Null(result);
        }

        [Fact]
        public void Parse_HttpDate_Rfc1123Format_ParsesCorrectly()
        {
            var now = new DateTimeOffset(2026, 6, 16, 10, 0, 0, TimeSpan.Zero);
            // 300 seconds (5 minutes) in the future
            string httpDate = "Tue, 16 Jun 2026 10:05:00 GMT";

            int? result = RetryAfterParser.Parse(httpDate, now);

            Assert.Equal(300, result);
        }

        [Fact]
        public void Parse_InvalidString_ReturnsNull()
        {
            Assert.Null(RetryAfterParser.Parse("not-a-date-or-number"));
        }

        [Fact]
        public void Parse_Zero_ReturnsZero()
        {
            Assert.Equal(0, RetryAfterParser.Parse("0"));
        }
    }
}
