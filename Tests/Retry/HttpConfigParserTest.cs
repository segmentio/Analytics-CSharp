using Segment.Analytics.Retry;
using Segment.Serialization;
using Xunit;

namespace Tests.Retry
{
    public class HttpConfigParserTest
    {
        [Fact]
        public void Parse_Null_ReturnsNull()
        {
            Assert.Null(HttpConfigParser.Parse(null));
        }

        [Fact]
        public void Parse_EmptyObject_DefaultsEnabledTrue()
        {
            var json = JsonUtility.FromJson<JsonObject>("{}");
            HttpConfig config = HttpConfigParser.Parse(json);

            Assert.NotNull(config);
            Assert.True(config.RateLimitConfig.Enabled);
            Assert.True(config.BackoffConfig.Enabled);
        }

        [Fact]
        public void Parse_ExplicitEnabled_Respected()
        {
            var json = JsonUtility.FromJson<JsonObject>(
                "{\"rateLimitConfig\":{\"enabled\":\"false\"},\"backoffConfig\":{\"enabled\":\"true\"}}");
            HttpConfig config = HttpConfigParser.Parse(json);

            Assert.False(config.RateLimitConfig.Enabled);
            Assert.True(config.BackoffConfig.Enabled);
        }

        [Fact]
        public void Parse_BackoffConfig_ParsesValues()
        {
            var json = JsonUtility.FromJson<JsonObject>(
                "{\"backoffConfig\":{\"maxRetryCount\":\"5\",\"baseBackoffInterval\":\"1.0\",\"maxBackoffInterval\":\"60\"}}");
            HttpConfig config = HttpConfigParser.Parse(json);

            Assert.Equal(5, config.BackoffConfig.MaxRetryCount);
            Assert.Equal(1.0, config.BackoffConfig.BaseBackoffInterval);
            Assert.Equal(60, config.BackoffConfig.MaxBackoffInterval);
        }

        [Fact]
        public void Parse_RateLimitConfig_ParsesValues()
        {
            var json = JsonUtility.FromJson<JsonObject>(
                "{\"rateLimitConfig\":{\"maxRetryCount\":\"10\",\"maxRetryInterval\":\"120\"}}");
            HttpConfig config = HttpConfigParser.Parse(json);

            Assert.Equal(10, config.RateLimitConfig.MaxRetryCount);
            Assert.Equal(120, config.RateLimitConfig.MaxRetryInterval);
        }

        [Fact]
        public void Parse_StatusCodeOverrides_Parsed()
        {
            var json = JsonUtility.FromJson<JsonObject>(
                "{\"backoffConfig\":{\"statusCodeOverrides\":{\"400\":\"retry\",\"500\":\"drop\"}}}");
            HttpConfig config = HttpConfigParser.Parse(json);

            Assert.Equal(RetryBehavior.Retry, config.BackoffConfig.StatusCodeOverrides[400]);
            Assert.Equal(RetryBehavior.Drop, config.BackoffConfig.StatusCodeOverrides[500]);
        }

        [Fact]
        public void Parse_InvalidStatusCodeOverrides_Filtered()
        {
            var json = JsonUtility.FromJson<JsonObject>(
                "{\"backoffConfig\":{\"statusCodeOverrides\":{\"abc\":\"retry\",\"999\":\"retry\",\"200\":\"invalid\"}}}");
            HttpConfig config = HttpConfigParser.Parse(json);

            Assert.Empty(config.BackoffConfig.StatusCodeOverrides);
        }

        [Fact]
        public void Parse_ClampsValues()
        {
            var json = JsonUtility.FromJson<JsonObject>(
                "{\"rateLimitConfig\":{\"maxRetryCount\":\"9999\",\"maxRetryInterval\":\"99999\"}," +
                "\"backoffConfig\":{\"baseBackoffInterval\":\"999\",\"maxBackoffInterval\":\"99999\"}}");
            HttpConfig config = HttpConfigParser.Parse(json);

            Assert.Equal(1000, config.RateLimitConfig.MaxRetryCount);
            Assert.Equal(3600, config.RateLimitConfig.MaxRetryInterval);
            Assert.Equal(60.0, config.BackoffConfig.BaseBackoffInterval);
            Assert.Equal(3600, config.BackoffConfig.MaxBackoffInterval);
        }

        [Fact]
        public void Parse_PartialConfig_UsesDefaults()
        {
            var json = JsonUtility.FromJson<JsonObject>(
                "{\"backoffConfig\":{\"maxRetryCount\":\"50\"}}");
            HttpConfig config = HttpConfigParser.Parse(json);

            Assert.Equal(50, config.BackoffConfig.MaxRetryCount);
            Assert.Equal(0.5, config.BackoffConfig.BaseBackoffInterval); // default
            Assert.Equal(300, config.BackoffConfig.MaxBackoffInterval); // default
        }
    }
}
