using System;
using System.Collections.Generic;

namespace Segment.Analytics.Retry
{
    public class RateLimitConfig
    {
        /// <summary>Largest Retry-After the client will honour, in seconds. RFC 7231 allows
        /// more, but the TAPI agreements cap it here and the other SDKs fix it at this value.</summary>
        public const int MaxRetryIntervalCeiling = 300;

        public bool Enabled { get; }
        public int MaxRetryCount { get; }
        public int MaxRetryInterval { get; }

        /// <summary>
        /// Wall-clock ceiling, in seconds, on how long one rate-limit episode may keep a
        /// batch alive. A last-ditch guard so a pathological Retry-After stream cannot hold
        /// a batch forever; <see cref="MaxRetryCount"/> is what stops retrying in practice.
        /// At the defaults the count is reached first by a wide margin, since
        /// MaxRetryCount * MaxRetryIntervalCeiling is well under this.
        /// </summary>
        public long MaxRateLimitDuration { get; }

        public RateLimitConfig(
            bool enabled = true,
            int maxRetryCount = 100,
            int maxRetryInterval = 300,
            long maxRateLimitDuration = 43200)
        {
            Enabled = enabled;
            MaxRetryCount = maxRetryCount;
            MaxRetryInterval = maxRetryInterval;
            MaxRateLimitDuration = maxRateLimitDuration;
        }

        public RateLimitConfig Validated() => new RateLimitConfig(
            enabled: Enabled,
            // Floored at 1: the count is compared against a fresh state's retry
            // count, so 0 would drop every batch before it was ever sent.
            maxRetryCount: Math.Max(1, Math.Min(MaxRetryCount, 1000)),
            maxRetryInterval: Math.Max(1, Math.Min(MaxRetryInterval, MaxRetryIntervalCeiling)),
            maxRateLimitDuration: Math.Max(0, Math.Min(MaxRateLimitDuration, 604800))
        );
    }

    public class BackoffConfig
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
            bool enabled = true,
            int maxRetryCount = 10,
            double baseBackoffInterval = 0.5,
            int maxBackoffInterval = 60,
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
            // Merged over the defaults, not substituted for them. Replacing meant that
            // overriding one status silently changed seven others: 408, 410, 429 and
            // 460 stopped being retried, and 511 fell through to Default5xxBehavior
            // and started being retried, which is the one thing it must never do.
            // Copied rather than aliased because the property is public, so sharing
            // the static default would let one caller's mutation corrupt every
            // BackoffConfig built afterwards.
            StatusCodeOverrides = new Dictionary<int, RetryBehavior>(DefaultStatusCodeOverrides);
            if (statusCodeOverrides != null)
            {
                foreach (KeyValuePair<int, RetryBehavior> kvp in statusCodeOverrides)
                    StatusCodeOverrides[kvp.Key] = kvp.Value;
            }
        }

        public BackoffConfig Validated() => new BackoffConfig(
            enabled: Enabled,
            maxRetryCount: Math.Max(1, Math.Min(MaxRetryCount, 1000)),
            baseBackoffInterval: Math.Max(0.1, Math.Min(BaseBackoffInterval, 60.0)),
            maxBackoffInterval: Math.Max(1, Math.Min(MaxBackoffInterval, 3600)),
            // Floored at 1 for the same reason as maxRetryCount: ExceedsMaxDuration
            // compares elapsed time against this, so 0 meant "no budget" — the batch
            // was abandoned on its second attempt — rather than "no cap".
            maxTotalBackoffDuration: Math.Max(1, Math.Min(MaxTotalBackoffDuration, 604800)),
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
                { 505, RetryBehavior.Drop },
                // 511 is only retryable for an SDK that can re-authenticate via OAuth.
                // This one cannot, so retrying would spend the budget on a request that
                // can never succeed.
                { 511, RetryBehavior.Drop }
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

    public class HttpConfig
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
