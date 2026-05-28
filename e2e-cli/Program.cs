// e2e-cli: End-to-end test CLI for Analytics-CSharp
// Reads --input <json> from args, sends events via the SDK, outputs JSON result to stdout.
// Debug/info logs go to stderr.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Segment.Analytics;
using Segment.Analytics.Plugins;
using Segment.Analytics.Retry;
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
int maxRetries = 100;
int timeoutSeconds = 20;
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
    if (configEl.TryGetProperty("timeout", out var to)) timeoutSeconds = to.GetInt32();
}

// ── Error handler ────────────────────────────────────────────────────────────
var deliveryErrors = new List<string>();
var errorHandler = new CapturingErrorHandler(deliveryErrors);

// ── Build configuration ──────────────────────────────────────────────────────

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

// Enable smart retry directly via a custom pipeline provider (same approach as Kotlin e2e-cli).
var retryHttpConfig = new HttpConfig(
    new RateLimitConfig(enabled: true, maxRetryCount: maxRetries),
    new BackoffConfig(enabled: true, maxRetryCount: maxRetries, baseBackoffInterval: 0.5)
);
var pipelineProvider = new RetryEnabledPipelineProvider(retryHttpConfig);

var configBuilder = new Configuration(
    writeKey,
    flushAt: flushAt,
    flushInterval: flushInterval,
    analyticsErrorHandler: errorHandler,
    storageProvider: new InMemoryStorageProvider(),
    apiHost: rawApiHost,
    cdnHost: rawCdnHost,
    httpClientProvider: httpClientProvider,
    eventPipelineProvider: pipelineProvider
);

Console.Error.WriteLine($"[e2e-cli] Initialising analytics (writeKey={writeKey[..Math.Min(8, writeKey.Length)]}…, apiHost={apiHost ?? "default"}, maxRetries={maxRetries})");

var analytics = new Analytics(configBuilder);

// Wait for SDK to initialize (settings fetch, pipeline start/stop cycle).
// This prevents duplicate uploads from the pipeline restart during init.
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
                    analytics.Track(eventName, properties);
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
                    analytics.Page(title, properties, category);
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
                    analytics.Screen(title, properties, category);
                    break;
                }

                case "alias":
                {
                    string newId = userId ?? (ev.TryGetProperty("newId", out var newIdEl)
                        ? newIdEl.GetString() ?? ""
                        : "");
                    analytics.Alias(newId);
                    break;
                }

                case "group":
                {
                    string groupId = ev.TryGetProperty("groupId", out var gidEl)
                        ? gidEl.GetString() ?? ""
                        : "";
                    JsonObject? traits = GetJsonObject(ev, "traits");
                    analytics.Group(groupId, traits);
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

// ── Flush and poll until delivery completes ──────────────────────────────────
Console.Error.WriteLine($"[e2e-cli] Flushing {totalEvents} event(s)…");
deliveryErrors.Clear();

// The SDK's CountFlushPolicy (flushAt) auto-triggers uploads.
// We trigger one explicit flush to handle cases where events haven't been flushed yet,
// then poll and only trigger retries when pending files persist across cycles.
analytics.Flush();

// Poll until batch files are processed (uploaded or dropped).
long deadlineMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (timeoutSeconds * 1000L);
bool everSeenPending = false;
int pollInterval = 300;
int pollCount = 0;
int stableEmptyCount = 0;

while (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < deadlineMs)
{
    Thread.Sleep(pollInterval);
    pollCount++;

    var pending = analytics.PendingUploads()
        .Where(s => !string.IsNullOrEmpty(s))
        .ToList();

    if (pending.Count > 0)
    {
        everSeenPending = true;
        stableEmptyCount = 0;
        deliveryErrors.Clear();
        // Trigger a new upload cycle for retry
        analytics.Flush();
    }
    else if (everSeenPending)
    {
        // Files gone — wait for a stable "empty" state to confirm upload completed
        stableEmptyCount++;
        if (stableEmptyCount >= 2)
            break;
    }

    // Adaptive intervals
    if (pollCount >= 10 && pollInterval < 1000) pollInterval = 1000;
    else if (pollCount >= 5 && pollInterval < 500) pollInterval = 500;
}

// ── Output result ─────────────────────────────────────────────────────────────
var remaining = analytics.PendingUploads()
    .Where(s => !string.IsNullOrEmpty(s))
    .ToList();

bool success;
string? error = null;

if (remaining.Count > 0)
{
    success = false;
    error = $"Delivery incomplete: {remaining.Count} batch file(s) still pending";
}
else if (deliveryErrors.Count > 0)
{
    success = false;
    error = "Delivery failed: " + string.Join("; ", deliveryErrors);
}
else
{
    success = true;
}

if (success)
{
    Console.WriteLine($"{{\"success\":true,\"sentBatches\":1}}");
    Environment.Exit(0);
}
else
{
    Console.WriteLine($"{{\"success\":false,\"sentBatches\":0,\"error\":\"{Escape(error ?? "unknown")}\"}}");
    Environment.Exit(1);
}

// ── Helpers ───────────────────────────────────────────────────────────────────

static JsonObject? GetJsonObject(System.Text.Json.JsonElement parent, string key)
{
    if (!parent.TryGetProperty(key, out var el) || el.ValueKind == JsonValueKind.Null)
        return null;

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

// ── Pipeline provider that enables retry from construction ────────────────────

class RetryEnabledPipelineProvider : Segment.Analytics.Utilities.IEventPipelineProvider
{
    private readonly HttpConfig _httpConfig;
    public RetryEnabledPipelineProvider(HttpConfig httpConfig) => _httpConfig = httpConfig;

    public Segment.Analytics.Utilities.IEventPipeline Create(Analytics analytics, string key)
    {
        return new EventPipeline(analytics, key,
            analytics.Configuration.WriteKey,
            analytics.Configuration.FlushPolicies,
            analytics.Configuration.ApiHost,
            _httpConfig);
    }
}
