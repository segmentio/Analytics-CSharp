# Changelog

Release notes for published versions are generated on the
[Releases page](https://github.com/segmentio/Analytics-CSharp/releases).
This file carries the notes that need more than a pull-request title.

## Unreleased

### Behavior change: retries and backoff are on by default

Through 2.6.0, rate limiting and exponential backoff were both disabled unless you
supplied an `HttpConfig` or a CDN settings payload turned them on. Server-side
deployments receive no CDN settings, so in practice they retried nothing: 408, 410 and
460 were dropped, `Retry-After` was ignored, and a 429 or 5xx was held with no delay and
no budget. Both subsystems now default to enabled, so a client that configures nothing
gets the documented retry behavior.

To keep the old behavior, disable both explicitly:

```csharp
new Configuration("writeKey")
{
    HttpConfig = new HttpConfig(
        new RateLimitConfig(enabled: false),
        new BackoffConfig(enabled: false))
}
```

CDN settings are unaffected and still take precedence: a payload carrying an
`httpConfig` key replaces whatever the pipeline is running with, and a payload without
that key leaves your configuration in effect.

- Backoff defaults now match the other Segment SDKs: `MaxRetryCount` 10 (was 100) and `MaxBackoffInterval` 60s (was 300s). With retries off by default those numbers were latent; enabling them unchanged would have had C# clients making an order of magnitude more attempts against the endpoint than any other SDK.

### Upgrade note: new request headers and proxy allowlists

This release sends two request headers that 2.6.0 did not: `Authorization`
(HTTP Basic, carrying your write key) and `X-Retry-Count` (on retries only).
If your traffic to Segment goes through a proxy, gateway or WAF that
allowlists request headers, add both before upgrading or uploads will be
rejected. Unity WebGL builds must also add them to the CORS
`Access-Control-Allow-Headers` allowlist on any proxy they point at.

- Send the write key as an `Authorization: Basic` header. It is still included in the request body, so no server-side change is required.
- Send `X-Retry-Count` on retries, so the server can distinguish a retry from a first attempt.
- `HttpConfig` is now a settable property on `Configuration` rather than a constructor parameter, so retry behavior can be configured after construction. For mobile targets, CDN settings replace `Configuration.HttpConfig` when they are present.
- `Retry-After` is honoured on every retryable status rather than 429 alone, which brings 529 in through the generic 5xx rule. Numeric seconds and the RFC 7231 HTTP-date formats are both accepted, capped at `MaxRetryInterval`.
- New `RateLimitConfig.MaxRateLimitDuration` (default 12 hours) bounds how long a single rate-limit episode can keep a batch alive. Every other Segment SDK already had this; C# bounded the rate-limit path by a retry count alone. The count still stops retrying in practice — at the defaults it is reached long before the duration.
- `MaxRetryInterval` is now capped at 300s rather than 3600s, matching the fixed 300s ceiling in the other SDKs.
- 511 is dropped rather than retried: it asks the client to authenticate, which this library cannot do.
- Only 2xx responses count as a successful upload. A 3xx is now reported as a failed upload rather than silently treated as delivered. It is not retried: a redirect the HTTP client already declined to follow will not succeed on a retry. The Segment endpoint does not redirect, so this only affects custom host values.
- `RateLimitConfig.MaxRetryCount` and `BackoffConfig.MaxRetryCount` are floored at 1. A configured 0 previously dropped every batch before it was ever sent.
- `BackoffConfig.StatusCodeOverrides` is copied rather than held by reference, so mutating the caller's dictionary no longer changes a live config.
