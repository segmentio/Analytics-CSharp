using System;
using System.Collections.Generic;

namespace Segment.Analytics.Retry
{
    internal class RateLimitConfig
    {
        public bool Enabled { get; }
        public int MaxRetryCount { get; }
        public int MaxRetryInterval { get; }

        public RateLimitConfig(bool enabled = false, int maxRetryCount = 100, int maxRetryInterval = 300)
        {
            Enabled = enabled;
            MaxRetryCount = maxRetryCount;
            MaxRetryInterval = maxRetryInterval;
        }

        public RateLimitConfig Validated() => new RateLimitConfig(
            enabled: Enabled,
            maxRetryCount: Math.Max(0, Math.Min(MaxRetryCount, 1000)),
            maxRetryInterval: Math.Max(1, Math.Min(MaxRetryInterval, 3600))
        );
    }

    internal class BackoffConfig
    {
        public bool Enabled { get; }
        public int MaxRetryCount { get; }
        public double BaseBackoffInterval { get; }
        public int MaxBackoffInterval { get; }
        public long MaxTotalBackoffDuration { get; }
        public int JitterPercent { get; }
        public RetryBehavior Default4xxBehavior { get; }
        public RetryBehavior Default5xxBehavior { get; }
        public RetryBehavior UnknownCodeBehavior { get; }
        public Dictionary<int, RetryBehavior> StatusCodeOverrides { get; }

        public BackoffConfig(
            bool enabled = false,
            int maxRetryCount = 100,
            double baseBackoffInterval = 0.5,
            int maxBackoffInterval = 300,
            long maxTotalBackoffDuration = 43200,
            int jitterPercent = 10,
            RetryBehavior default4xxBehavior = RetryBehavior.Drop,
            RetryBehavior default5xxBehavior = RetryBehavior.Retry,
            RetryBehavior unknownCodeBehavior = RetryBehavior.Drop,
            Dictionary<int, RetryBehavior> statusCodeOverrides = null)
        {
            Enabled = enabled;
            MaxRetryCount = maxRetryCount;
            BaseBackoffInterval = baseBackoffInterval;
            MaxBackoffInterval = maxBackoffInterval;
            MaxTotalBackoffDuration = maxTotalBackoffDuration;
            JitterPercent = jitterPercent;
            Default4xxBehavior = default4xxBehavior;
            Default5xxBehavior = default5xxBehavior;
            UnknownCodeBehavior = unknownCodeBehavior;
            StatusCodeOverrides = statusCodeOverrides ?? DefaultStatusCodeOverrides;
        }

        public BackoffConfig Validated() => new BackoffConfig(
            enabled: Enabled,
            maxRetryCount: Math.Max(0, Math.Min(MaxRetryCount, 1000)),
            baseBackoffInterval: Math.Max(0.1, Math.Min(BaseBackoffInterval, 60.0)),
            maxBackoffInterval: Math.Max(1, Math.Min(MaxBackoffInterval, 3600)),
            maxTotalBackoffDuration: Math.Max(0, Math.Min(MaxTotalBackoffDuration, 604800)),
            jitterPercent: Math.Max(0, Math.Min(JitterPercent, 50)),
            default4xxBehavior: Default4xxBehavior,
            default5xxBehavior: Default5xxBehavior,
            unknownCodeBehavior: UnknownCodeBehavior,
            statusCodeOverrides: ValidateOverrides(StatusCodeOverrides)
        );

        private static Dictionary<int, RetryBehavior> ValidateOverrides(
            Dictionary<int, RetryBehavior> overrides)
        {
            var result = new Dictionary<int, RetryBehavior>();
            foreach (var kvp in overrides)
            {
                if (kvp.Key >= 100 && kvp.Key <= 599)
                    result[kvp.Key] = kvp.Value;
            }
            return result;
        }

        private static readonly Dictionary<int, RetryBehavior> DefaultStatusCodeOverrides =
            new Dictionary<int, RetryBehavior>
            {
                { 408, RetryBehavior.Retry },
                { 410, RetryBehavior.Retry },
                { 429, RetryBehavior.Retry },
                { 460, RetryBehavior.Retry },
                { 501, RetryBehavior.Drop },
                { 505, RetryBehavior.Drop }
            };
    }

    internal class RetryConfig
    {
        public RateLimitConfig RateLimitConfig { get; }
        public BackoffConfig BackoffConfig { get; }

        public RetryConfig(RateLimitConfig rateLimitConfig = null, BackoffConfig backoffConfig = null)
        {
            RateLimitConfig = rateLimitConfig ?? new RateLimitConfig();
            BackoffConfig = backoffConfig ?? new BackoffConfig();
        }
    }

    internal class HttpConfig
    {
        public RateLimitConfig RateLimitConfig { get; }
        public BackoffConfig BackoffConfig { get; }

        public HttpConfig(RateLimitConfig rateLimitConfig = null, BackoffConfig backoffConfig = null)
        {
            RateLimitConfig = rateLimitConfig ?? new RateLimitConfig();
            BackoffConfig = backoffConfig ?? new BackoffConfig();
        }
    }
}
