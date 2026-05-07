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

JsonElement root = doc.RootElement;

string writeKey = root.GetProperty("writeKey").GetString()
    ?? throw new InvalidOperationException("writeKey is required");

string? apiHost = root.TryGetProperty("apiHost", out var apiHostEl) ? apiHostEl.GetString() : null;

// config block (optional)
int flushAt = 15;
int flushInterval = 10; // seconds
if (root.TryGetProperty("config", out var configEl))
{
    if (configEl.TryGetProperty("flushAt", out var fa)) flushAt = fa.GetInt32();
    if (configEl.TryGetProperty("flushInterval", out var fi))
    {
        // input is in ms; SDK expects seconds
        int fiMs = fi.GetInt32();
        flushInterval = Math.Max(1, fiMs / 1000);
    }
}

// ── Error handler ────────────────────────────────────────────────────────────
var errors = new List<string>();
var errorHandler = new CapturingErrorHandler(errors);

// ── Build configuration ──────────────────────────────────────────────────────
var configBuilder = new Configuration(
    writeKey,
    flushAt: flushAt,
    flushInterval: flushInterval,
    analyticsErrorHandler: errorHandler,
    storageProvider: new InMemoryStorageProvider(),
    apiHost: apiHost
);

Console.Error.WriteLine($"[e2e-cli] Initialising analytics (writeKey={writeKey[..Math.Min(8, writeKey.Length)]}…, apiHost={apiHost ?? "default"})");

var analytics = new Analytics(configBuilder);

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
                    // Set userId state first if provided
                    if (userId != null && analytics.UserId() != userId)
                        analytics.Identify(userId);

                    string eventName = ev.TryGetProperty("event", out var enEl)
                        ? enEl.GetString() ?? "Unknown"
                        : "Unknown";
                    JsonObject? properties = GetJsonObject(ev, "properties");
                    analytics.Track(eventName, properties);
                    break;
                }

                case "page":
                {
                    if (userId != null && analytics.UserId() != userId)
                        analytics.Identify(userId);

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
                    if (userId != null && analytics.UserId() != userId)
                        analytics.Identify(userId);

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
                    // For alias: previousId becomes the current userId state, newId is the alias target.
                    // The SDK Alias(newId) uses _userInfo._userId as previousId.
                    string? previousId = ev.TryGetProperty("previousId", out var prevEl)
                        ? prevEl.GetString()
                        : null;
                    string newId = userId ?? (ev.TryGetProperty("newId", out var newIdEl)
                        ? newIdEl.GetString() ?? ""
                        : "");

                    if (previousId != null && analytics.UserId() != previousId)
                        analytics.Identify(previousId);

                    analytics.Alias(newId);
                    break;
                }

                case "group":
                {
                    if (userId != null && analytics.UserId() != userId)
                        analytics.Identify(userId);

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

Console.Error.WriteLine($"[e2e-cli] Flushing {totalEvents} event(s)…");
analytics.Flush();

// Give the async pipeline time to upload
Thread.Sleep(5000);

// ── Output result ─────────────────────────────────────────────────────────────
bool success = errors.Count == 0;
if (success)
{
    Console.WriteLine($"{{\"success\":true,\"sentBatches\":1}}");
    Environment.Exit(0);
}
else
{
    string combinedErrors = string.Join("; ", errors);
    Console.WriteLine($"{{\"success\":false,\"sentBatches\":0,\"error\":\"{Escape(combinedErrors)}\"}}");
    Environment.Exit(1);
}

// ── Helpers ───────────────────────────────────────────────────────────────────

static JsonObject? GetJsonObject(JsonElement parent, string key)
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
