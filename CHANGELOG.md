# Changelog

Release notes for published versions are generated on the
[Releases page](https://github.com/segmentio/Analytics-CSharp/releases).
This file carries the notes that need more than a pull-request title.

## Unreleased

### Upgrade note: new request headers and proxy allowlists

This release sends two request headers that 2.6.0 did not: `Authorization`
(HTTP Basic, carrying your write key) and `X-Retry-Count` (on retries only).
If your traffic to Segment goes through a proxy, gateway or WAF that
allowlists request headers, add both before upgrading or uploads will be
rejected. Unity WebGL builds must also add them to the CORS
`Access-Control-Allow-Headers` allowlist on any proxy they point at.

- Send the write key as an `Authorization: Basic` header. It is still included in the request body, so no server-side change is required.
- Send `X-Retry-Count` on retries, so the server can distinguish a retry from a first attempt.
- `HttpConfig` is now a settable property on `Configuration` rather than a constructor parameter, so retry behaviour can be configured after construction. For mobile targets, CDN settings replace `Configuration.HttpConfig` when they are present.
- `Retry-After` is honoured on every retryable status rather than 429 alone, which brings 529 in through the generic 5xx rule. Numeric seconds and the RFC 7231 HTTP-date formats are both accepted, capped at `MaxRetryInterval`.
- 511 is dropped rather than retried: it asks the client to authenticate, which this library cannot do.
- Only 2xx responses count as a successful upload. A 3xx is now reported as a failed upload rather than silently treated as delivered. It is not retried: a redirect the HTTP client already declined to follow will not succeed on a retry. The Segment endpoint does not redirect, so this only affects custom host values.
- `RateLimitConfig.MaxRetryCount` and `BackoffConfig.MaxRetryCount` are floored at 1. A configured 0 previously dropped every batch before it was ever sent.
- `BackoffConfig.StatusCodeOverrides` is copied rather than held by reference, so mutating the caller's dictionary no longer changes a live config.
