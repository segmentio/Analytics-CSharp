using System;
using System.Globalization;

namespace Segment.Analytics.Utilities
{
    internal static class RetryAfterParser
    {
        /// <summary>
        /// Parses a Retry-After header value. Supports both integer seconds and HTTP-date (RFC 1123) format.
        /// Returns the number of seconds to wait, or null if the header is empty/unparseable/in the past.
        /// </summary>
        internal static int? Parse(string headerValue, DateTimeOffset? now = null)
        {
            if (string.IsNullOrEmpty(headerValue))
                return null;

            string trimmed = headerValue.Trim();

            if (int.TryParse(trimmed, out int parsedInt))
            {
                return parsedInt;
            }

            if (DateTimeOffset.TryParseExact(trimmed,
                new[] { "r", "ddd, dd MMM yyyy HH:mm:ss 'GMT'" },
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out DateTimeOffset targetDate))
            {
                DateTimeOffset reference = now ?? DateTimeOffset.UtcNow;
                int seconds = (int)(targetDate - reference).TotalSeconds;
                return seconds > 0 ? seconds : (int?)null;
            }

            return null;
        }
    }
}
