using System;
using System.Text;
using Segment.Analytics.Utilities;
using Xunit;

namespace Tests.Utilities
{
    /// <summary>
    /// TAPI authenticates and routes on the Authorization header rather than parsing the
    /// payload, so the value has to match what the other Segment SDKs send: the write key
    /// as Basic credentials with an empty password.
    /// </summary>
    public class HttpClientAuthTest
    {
        private class AuthProbe : DefaultHTTPClient
        {
            public AuthProbe(string apiKey) : base(apiKey) { }

            public string Authorization => BasicAuthorization;
        }

        [Theory]
        [InlineData("writekey123")]
        [InlineData("aBc-123_XYZ")]
        public void UsesWriteKeyAsBasicCredentialsWithEmptyPassword(string writeKey)
        {
            var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(writeKey + ":"));

            Assert.Equal(expected, new AuthProbe(writeKey).Authorization);
        }

        [Fact]
        public void EncodesTheTrailingColonSeparator()
        {
            // Decoding must yield "<writeKey>:" — an empty password, not a missing one.
            var header = new AuthProbe("k").Authorization;
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header.Substring("Basic ".Length)));

            Assert.Equal("k:", decoded);
        }
    }
}
