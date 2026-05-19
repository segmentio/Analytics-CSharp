// e2e-cli: End-to-end test CLI for Analytics-CSharp
// Reads --input <json> from args, sends events via the SDK, outputs JSON result to stdout.
// Debug/info logs go to stderr.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using Segment.Analytics;
using Segment.Analytics.Utilities;
using Segment.Serialization;
using JsonUtility = Segment.Serialization.JsonUtility;

// ── Argument parsing ────────────────────────────────────────────────────────
string? inputJson = null;
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--input")
    {
        inputJson = args[i + 1];
        break;
    }
}

if (inputJson == null)
{
    Console.Error.WriteLine("[e2e-cli] ERROR: --input <json> argument is required");
    Console.WriteLine("{\"success\":false,\"error\":\"--input argument is required\"}");
    Environment.Exit(1);
}

// ── Parse the input JSON ─────────────────────────────────────────────────────
JsonDocument doc;
try
{
    doc = JsonDocument.Parse(inputJson);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[e2e-cli] ERROR: Failed to parse --input JSON: {ex.Message}");
    Console.WriteLine($"{{\"success\":false,\"error\":\"Failed to parse input JSON: {Escape(ex.Message)}\"}}");
    Environment.Exit(1);
    return; // unreachable, satisfies compiler
}

System.Text.Json.JsonElement root = doc.RootElement;

string writeKey = root.GetProperty("writeKey").GetString()
    ?? throw new InvalidOperationException("writeKey is required");

string? apiHost = root.TryGetProperty("apiHost", out var apiHostEl) ? apiHostEl.GetString() : null;
string? cdnHost = root.TryGetProperty("cdnHost", out var cdnHostEl) ? cdnHostEl.GetString() : apiHost;

// config block (optional)
int flushAt = 15;
int flushInterval = 10; // seconds
int maxRetries = 10;
if (root.TryGetProperty("config", out var configEl))
{
    if (configEl.TryGetProperty("flushAt", out var fa)) flushAt = fa.GetInt32();
    if (configEl.TryGetProperty("flushInterval", out var fi))
    {
        // input is in ms; SDK expects seconds
        int fiMs = fi.GetInt32();
        flushInterval = Math.Max(1, fiMs / 1000);
    }
    if (configEl.TryGetProperty("maxRetries", out var mr)) maxRetries = mr.GetInt32();
}

// ── Logger (captures retry-exhaustion errors) ─────────────────────────────────
var capturingLogger = new CapturingLogger();
Analytics.Logger = capturingLogger;

// ── Error handler ────────────────────────────────────────────────────────────
var errors = new List<string>();
var errorHandler = new CapturingErrorHandler(errors);

// ── Build configuration ──────────────────────────────────────────────────────

// Determine scheme from apiHost so we can override SegmentURL for http:// targets
// (the SDK always prepends "https://" by default).
// Determine scheme and strip it — the SDK prepends scheme via SegmentURL,
// which we override in PlainHttpClient to respect http:// targets.
string scheme = "https://";
string? rawApiHost = apiHost;
string? rawCdnHost = cdnHost;

if (apiHost != null && apiHost.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
{
    scheme = "http://";
    rawApiHost = apiHost.Substring("http://".Length).TrimEnd('/');
}
else if (apiHost != null && apiHost.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
{
    rawApiHost = apiHost.Substring("https://".Length).TrimEnd('/');
}

// Strip scheme from cdnHost too (same PlainHttpClient handles it)
if (cdnHost != null && cdnHost.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
    rawCdnHost = cdnHost.Substring("http://".Length).TrimEnd('/');
else if (cdnHost != null && cdnHost.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    rawCdnHost = cdnHost.Substring("https://".Length).TrimEnd('/');

var httpClientProvider = new PlainHttpClientProvider(scheme);

var configBuilder = new Configuration(
    writeKey,
    flushAt: flushAt,
    flushInterval: flushInterval,
    analyticsErrorHandler: errorHandler,
    storageProvider: new InMemoryStorageProvider(),
    apiHost: rawApiHost,
    cdnHost: rawCdnHost,
    httpClientProvider: httpClientProvider,
    maxRetries: maxRetries
);

Console.Error.WriteLine($"[e2e-cli] Initialising analytics (writeKey={writeKey[..Math.Min(8, writeKey.Length)]}…, apiHost={apiHost ?? "default"})");

var analytics = new Analytics(configBuilder);

// If AUTO_SETTINGS is enabled, wait briefly for the settings fetch to complete
// so that httpConfig overrides (BackoffEnabled, MaxRateLimitRetries, etc.) are
// applied before the first upload starts.
bool autoSettings = string.Equals(Environment.GetEnvironmentVariable("AUTO_SETTINGS"), "true",
    StringComparison.OrdinalIgnoreCase);
if (autoSettings)
    Thread.Sleep(2000);

// ── Process sequences ────────────────────────────────────────────────────────
int totalEvents = 0;

if (root.TryGetProperty("sequences", out var sequencesEl))
{
    foreach (var sequence in sequencesEl.EnumerateArray())
    {
        int delayMs = sequence.TryGetProperty("delayMs", out var delayEl) ? delayEl.GetInt32() : 0;
        if (delayMs > 0)
        {
            Console.Error.WriteLine($"[e2e-cli] Waiting {delayMs}ms before next sequence");
            Thread.Sleep(delayMs);
        }

        if (!sequence.TryGetProperty("events", out var eventsEl)) continue;

        foreach (var ev in eventsEl.EnumerateArray())
        {
            string eventType = ev.GetProperty("type").GetString()?.ToLowerInvariant() ?? "";
            string? userId = ev.TryGetProperty("userId", out var uidEl) ? uidEl.GetString() : null;

            Console.Error.WriteLine($"[e2e-cli] Sending event type={eventType} userId={userId ?? "(none)"}");

            switch (eventType)
            {
                case "identify":
                {
                    JsonObject? traits = GetJsonObject(ev, "traits");
                    if (userId != null)
                        analytics.Identify(userId, traits);
                    else
                        analytics.Identify(traits ?? new JsonObject());
                    break;
                }

                case "track":
                {
                    string eventName = ev.TryGetProperty("event", out var enEl)
                        ? enEl.GetString() ?? "Unknown"
                        : "Unknown";
                    JsonObject? properties = GetJsonObject(ev, "properties");
                    analytics.Track(eventName, properties, userId != null ? e => { ((TrackEvent)e).UserId = userId; return e; } : null);
                    break;
                }

                case "page":
                {
                    string title = ev.TryGetProperty("name", out var nameEl)
                        ? nameEl.GetString() ?? ""
                        : "";
                    string category = ev.TryGetProperty("category", out var catEl)
                        ? catEl.GetString() ?? ""
                        : "";
                    JsonObject? properties = GetJsonObject(ev, "properties");
                    analytics.Page(title, properties, category, userId != null ? e => { ((PageEvent)e).UserId = userId; return e; } : null);
                    break;
                }

                case "screen":
                {
                    string title = ev.TryGetProperty("name", out var nameEl)
                        ? nameEl.GetString() ?? ""
                        : "";
                    string category = ev.TryGetProperty("category", out var catEl)
                        ? catEl.GetString() ?? ""
                        : "";
                    JsonObject? properties = GetJsonObject(ev, "properties");
                    analytics.Screen(title, properties, category, userId != null ? e => { ((ScreenEvent)e).UserId = userId; return e; } : null);
                    break;
                }

                case "alias":
                {
                    string? previousId = ev.TryGetProperty("previousId", out var prevEl)
                        ? prevEl.GetString()
                        : null;
                    string newId = userId ?? (ev.TryGetProperty("newId", out var newIdEl)
                        ? newIdEl.GetString() ?? ""
                        : "");
                    analytics.Alias(newId, previousId != null ? e => { ((AliasEvent)e).PreviousId = previousId; return e; } : null);
                    break;
                }

                case "group":
                {
                    string groupId = ev.TryGetProperty("groupId", out var gidEl)
                        ? gidEl.GetString() ?? ""
                        : "";
                    JsonObject? traits = GetJsonObject(ev, "traits");
                    analytics.Group(groupId, traits, userId != null ? e => { ((GroupEvent)e).UserId = userId; return e; } : null);
                    break;
                }

                default:
                    Console.Error.WriteLine($"[e2e-cli] WARNING: Unknown event type '{eventType}', skipping");
                    continue;
            }

            totalEvents++;
        }
    }
}

Console.Error.WriteLine($"[e2e-cli] Flushing {totalEvents} event(s)…");
analytics.Flush();

// Wait for the async pipeline to finish flushing + retries.
// Cap at 25s so tests with a 30s timeout always get a result.
int waitMs = Math.Min(Math.Max(10_000, maxRetries * 2_000 + 5_000), 25_000);
Thread.Sleep(waitMs);

// ── Output result ─────────────────────────────────────────────────────────────
// Combine SDK error handler errors (non-retryable drops) with captured logger errors
// (retry exhaustion, backoff budget exceeded). Either signals final failure.
var logErrors = ((CapturingLogger)Analytics.Logger).Errors;
var allErrors = new List<string>(errors);
allErrors.AddRange(logErrors);

bool success = allErrors.Count == 0;
if (success)
{
    Console.WriteLine($"{{\"success\":true,\"sentBatches\":1}}");
    Environment.Exit(0);
}
else
{
    string combinedErrors = string.Join("; ", allErrors);
    Console.WriteLine($"{{\"success\":false,\"sentBatches\":0,\"error\":\"{Escape(combinedErrors)}\"}}");
    Environment.Exit(1);
}

// ── Helpers ───────────────────────────────────────────────────────────────────

static JsonObject? GetJsonObject(System.Text.Json.JsonElement parent, string key)
{
    if (!parent.TryGetProperty(key, out var el) || el.ValueKind == JsonValueKind.Null)
        return null;

    // Serialise the JsonElement back to a JSON string, then parse with Segment's JsonUtility
    string json = el.GetRawText();
    try
    {
        return JsonUtility.FromJson<JsonObject>(json);
    }
    catch
    {
        return null;
    }
}

static string Escape(string s) =>
    s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");

// ── HTTP client that respects the scheme (http:// vs https://) ───────────────

class PlainHttpClient : Segment.Analytics.Utilities.DefaultHTTPClient
{
    private readonly string _scheme;
    public PlainHttpClient(string apiKey, string scheme, string? apiHost, string? cdnHost)
        : base(apiKey, apiHost, cdnHost) => _scheme = scheme;

    public override string SegmentURL(string host, string path) => _scheme + host + path;
}

class PlainHttpClientProvider : Segment.Analytics.Utilities.IHTTPClientProvider
{
    private readonly string _scheme;
    public PlainHttpClientProvider(string scheme) => _scheme = scheme;

    public Segment.Analytics.Utilities.HTTPClient CreateHTTPClient(
        string apiKey, string? apiHost = null, string? cdnHost = null)
        => new PlainHttpClient(apiKey, _scheme, apiHost, cdnHost);
}

// ── Error handler implementation ──────────────────────────────────────────────

class CapturingErrorHandler : IAnalyticsErrorHandler
{
    private readonly List<string> _errors;

    public CapturingErrorHandler(List<string> errors) => _errors = errors;

    public void OnExceptionThrown(Exception e)
    {
        string msg = e.Message;
        Console.Error.WriteLine($"[e2e-cli] SDK ERROR: {msg}");
        _errors.Add(msg);
    }
}

// ── Logger that captures Error-level messages (retry exhaustion, etc.) ────────

class CapturingLogger : Segment.Analytics.Utilities.ISegmentLogger
{
    private readonly List<string> _errors = new List<string>();
    public IReadOnlyList<string> Errors => _errors;

    public void Log(Segment.Analytics.Utilities.LogLevel logLevel, Exception exception = null, string message = null)
    {
        string text = message ?? exception?.Message ?? "";
        Console.Error.WriteLine($"[analytics][{logLevel}] {text}");
        // Only capture final-failure messages, not transient per-attempt errors.
        // Transient errors look like "Error 500 uploading to url".
        // Final failures are "Retries exhausted..." and "Max total backoff...".
        if (logLevel == Segment.Analytics.Utilities.LogLevel.Error && !string.IsNullOrEmpty(text)
            && (text.StartsWith("Retries exhausted") || text.StartsWith("Max total backoff")))
        {
            _errors.Add(text);
        }
    }
}
