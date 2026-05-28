using System.Collections.Generic;
using System.Globalization;
using Segment.Serialization;

namespace Segment.Analytics.Retry
{
    internal static class HttpConfigParser
    {
        public static HttpConfig Parse(JsonObject httpConfigJson)
        {
            if (httpConfigJson == null)
                return null;

            JsonObject rateLimitJson = httpConfigJson.GetJsonObject("rateLimitConfig");
            JsonObject backoffJson = httpConfigJson.GetJsonObject("backoffConfig");

            // CDN-sourced config defaults enabled to true (presence implies active).
            // Only honor explicit enabled: false from CDN.
            bool rateLimitEnabled = true;
            if (rateLimitJson != null)
            {
                string enabledStr = rateLimitJson.GetString("enabled");
                if (enabledStr != null && bool.TryParse(enabledStr, out bool parsed))
                    rateLimitEnabled = parsed;
            }

            bool backoffEnabled = true;
            if (backoffJson != null)
            {
                string enabledStr = backoffJson.GetString("enabled");
                if (enabledStr != null && bool.TryParse(enabledStr, out bool parsed))
                    backoffEnabled = parsed;
            }

            RateLimitConfig rateLimitConfig = ParseRateLimitConfig(rateLimitJson, rateLimitEnabled);
            BackoffConfig backoffConfig = ParseBackoffConfig(backoffJson, backoffEnabled);

            return new HttpConfig(
                rateLimitConfig: rateLimitConfig.Validated(),
                backoffConfig: backoffConfig.Validated()
            );
        }

        private static RateLimitConfig ParseRateLimitConfig(JsonObject json, bool enabled)
        {
            if (json == null)
                return new RateLimitConfig(enabled: enabled);

            int maxRetryCount = 100;
            string maxRetriesStr = json.GetString("maxRetryCount");
            if (maxRetriesStr != null && int.TryParse(maxRetriesStr, out int parsedMaxRetries))
                maxRetryCount = parsedMaxRetries;

            int maxRetryInterval = 300;
            string intervalStr = json.GetString("maxRetryInterval");
            if (intervalStr != null && int.TryParse(intervalStr, out int parsedInterval))
                maxRetryInterval = parsedInterval;

            return new RateLimitConfig(
                enabled: enabled,
                maxRetryCount: maxRetryCount,
                maxRetryInterval: maxRetryInterval
            );
        }

        private static BackoffConfig ParseBackoffConfig(JsonObject json, bool enabled)
        {
            if (json == null)
                return new BackoffConfig(enabled: enabled);

            int maxRetryCount = 100;
            string maxRetriesStr = json.GetString("maxRetryCount");
            if (maxRetriesStr != null && int.TryParse(maxRetriesStr, out int parsedMaxRetries))
                maxRetryCount = parsedMaxRetries;

            double baseBackoffInterval = 0.5;
            string baseStr = json.GetString("baseBackoffInterval");
            if (baseStr != null && double.TryParse(baseStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedBase))
                baseBackoffInterval = parsedBase;

            int maxBackoffInterval = 300;
            string maxStr = json.GetString("maxBackoffInterval");
            if (maxStr != null && int.TryParse(maxStr, out int parsedMax))
                maxBackoffInterval = parsedMax;

            long maxTotalBackoffDuration = 43200;
            string durationStr = json.GetString("maxTotalBackoffDuration");
            if (durationStr != null && long.TryParse(durationStr, out long parsedDuration))
                maxTotalBackoffDuration = parsedDuration;

            int jitterPercent = 10;
            string jitterStr = json.GetString("jitterPercent");
            if (jitterStr != null && int.TryParse(jitterStr, out int parsedJitter))
                jitterPercent = parsedJitter;

            Dictionary<int, RetryBehavior> statusCodeOverrides = null;
            JsonObject overridesJson = json.GetJsonObject("statusCodeOverrides");
            if (overridesJson != null)
                statusCodeOverrides = ParseStatusCodeOverrides(overridesJson);

            return new BackoffConfig(
                enabled: enabled,
                maxRetryCount: maxRetryCount,
                baseBackoffInterval: baseBackoffInterval,
                maxBackoffInterval: maxBackoffInterval,
                maxTotalBackoffDuration: maxTotalBackoffDuration,
                jitterPercent: jitterPercent,
                statusCodeOverrides: statusCodeOverrides
            );
        }

        private static Dictionary<int, RetryBehavior> ParseStatusCodeOverrides(JsonObject json)
        {
            var result = new Dictionary<int, RetryBehavior>();
            foreach (string key in json.Keys)
            {
                if (!int.TryParse(key, out int code) || code < 100 || code > 599)
                    continue;
                string val = json.GetString(key);
                if (val == "retry")
                    result[code] = RetryBehavior.Retry;
                else if (val == "drop")
                    result[code] = RetryBehavior.Drop;
            }
            return result;
        }
    }
}
