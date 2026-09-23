# Changelog

Release notes for published versions are generated on the
[Releases page](https://github.com/segmentio/Analytics-CSharp/releases).
This file carries the notes that need more than a pull-request title.

## Unreleased

### Behaviour change: retries and backoff are on by default

Through 2.6.0, rate limiting and exponential backoff were both disabled unless an
`HttpConfig` was supplied or a CDN settings payload turned them on. Server-side
deployments receive no CDN settings, so in practice they retried nothing: 408, 410
and 460 were dropped, `Retry-After` was ignored, and a 429 or 5xx was held with no
delay and no budget. Both subsystems now default to enabled, so a client that
configures nothing gets the retry behaviour described below.

To keep the previous behaviour, disable both explicitly:

```csharp
new Configuration("writeKey")
{
    HttpConfig = new HttpConfig(
        new RateLimitConfig(enabled: false),
        new BackoffConfig(enabled: false))
}
```

CDN settings still take precedence: a payload carrying an `httpConfig` key replaces
whatever the pipeline is running with, and a payload without that key leaves the
supplied configuration in effect.

### Upgrade note: new request headers

This release sends two request headers that 2.6.0 did not: `Authorization`
(HTTP Basic, carrying the write key) and `X-Retry-Count` (on retries only). If
traffic to Segment passes through a proxy, gateway or WAF that allowlists request
headers, add both before upgrading or uploads will be rejected. Unity WebGL builds
must also add them to the CORS `Access-Control-Allow-Headers` allowlist on any
proxy they point at.

### Retry handling

- A `Retry-After` header is honoured on any retryable response, not only 429. Numeric seconds and the RFC 7231 HTTP-date formats are both accepted, and the value is capped at `RateLimitConfig.MaxRetryInterval` (default 60 seconds).
- Responses carrying `Retry-After` are retried for up to `RateLimitConfig.MaxRateLimitDuration` (default 5 minutes). Other failures use exponential backoff from 500ms to a 60 second ceiling, limited by `BackoffConfig.MaxRetryCount` (default 10) and by `BackoffConfig.MaxTotalBackoffDuration` (default 12 hours) as an upper bound.
- 511 is dropped rather than retried: it asks the client to re-authenticate, which this library cannot do.
- `RetryBehavior`, `RateLimitConfig`, `BackoffConfig` and `HttpConfig` are now public, and `HttpConfig` is a settable property on `Configuration`, so retry behaviour can be configured in code. On mobile targets, CDN settings replace it when present.
- `BackoffConfig.StatusCodeOverrides` is merged over the built-in defaults rather than replacing them, so overriding one status leaves the rest unchanged.
- Retry counts and interval limits are clamped to usable ranges rather than accepted as given.

### Other changes

- The write key is sent as an `Authorization: Basic` header. It remains in the request body, so no server-side change is required.
- `X-Retry-Count` is sent on retries, allowing the server to distinguish a retry from a first attempt.
- Only 2xx responses count as a successful upload. A 3xx is reported as a failed upload rather than treated as delivered, and is not retried: a redirect the HTTP client has already declined to follow will not succeed on one. The Segment endpoint does not redirect, so this affects only custom host values.
