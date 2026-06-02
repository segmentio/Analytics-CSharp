using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Segment.Analytics.Utilities;

namespace Segment.Analytics.Retry
{
    internal static class RetryStateStorage
    {
        public static void SaveRetryState(IStorage storage, RetryState state)
        {
            try
            {
                string json = SerializeState(state);
                storage.WritePrefs(StorageConstants.RetryState, json);
            }
            catch (Exception)
            {
                // Defensive: never crash on serialization failure
            }
        }

        public static RetryState LoadRetryState(IStorage storage)
        {
            try
            {
                string json = storage.Read(StorageConstants.RetryState);
                if (string.IsNullOrEmpty(json))
                    return new RetryState();
                return DeserializeState(json);
            }
            catch (Exception)
            {
                return new RetryState();
            }
        }

        public static void ClearRetryState(IStorage storage)
        {
            storage.Remove(StorageConstants.RetryState);
        }

        private static string SerializeState(RetryState state)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"pipelineState\":\"").Append(state.PipelineState.ToString()).Append("\"");
            if (state.WaitUntilTime.HasValue)
                sb.Append(",\"waitUntilTime\":\"").Append(state.WaitUntilTime.Value.ToString(CultureInfo.InvariantCulture)).Append("\"");
            sb.Append(",\"globalRetryCount\":\"").Append(state.GlobalRetryCount.ToString(CultureInfo.InvariantCulture)).Append("\"");

            if (state.BatchMetadata.Count > 0)
            {
                sb.Append(",\"batchMetadata\":{");
                bool first = true;
                foreach (var kvp in state.BatchMetadata)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append("\"").Append(EscapeJsonString(kvp.Key)).Append("\":{");
                    sb.Append("\"failureCount\":\"").Append(kvp.Value.FailureCount.ToString(CultureInfo.InvariantCulture)).Append("\"");
                    if (kvp.Value.NextRetryTime.HasValue)
                        sb.Append(",\"nextRetryTime\":\"").Append(kvp.Value.NextRetryTime.Value.ToString(CultureInfo.InvariantCulture)).Append("\"");
                    if (kvp.Value.FirstFailureTime.HasValue)
                        sb.Append(",\"firstFailureTime\":\"").Append(kvp.Value.FirstFailureTime.Value.ToString(CultureInfo.InvariantCulture)).Append("\"");
                    sb.Append("}");
                }
                sb.Append("}");
            }

            sb.Append("}");
            return sb.ToString();
        }

        private static string EscapeJsonString(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        // Reverse of EscapeJsonString. Walks the string once so a "\\" sequence
        // isn't re-interpreted as the start of another escape (which a naive
        // chained Replace would do).
        private static string UnescapeJsonString(string s)
        {
            if (s.IndexOf('\\') < 0) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    char next = s[i + 1];
                    if (next == '\\' || next == '"')
                    {
                        sb.Append(next);
                        i++;
                        continue;
                    }
                }
                sb.Append(s[i]);
            }
            return sb.ToString();
        }

        // Finds the index of the closing quote for a JSON string whose opening
        // quote is at openQuote, skipping any backslash-escaped character so an
        // escaped quote (\") doesn't terminate the scan early. Returns -1 if
        // unterminated.
        private static int FindClosingQuote(string json, int openQuote)
        {
            for (int i = openQuote + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '\\') { i++; continue; }
                if (c == '"') return i;
            }
            return -1;
        }

        private static RetryState DeserializeState(string json)
        {
            // Manual parsing to avoid Serialization.NET's numeric string coercion.
            // Format is well-defined since we control serialization.
            var fields = ParseJsonFields(json);

            PipelineState pipelineState = PipelineState.Ready;
            if (fields.TryGetValue("pipelineState", out string psVal))
            {
                // Current format is the enum name; "1" is the legacy ordinal for RateLimited.
                if (string.Equals(psVal, PipelineState.RateLimited.ToString(), StringComparison.Ordinal)
                    || psVal == "1")
                    pipelineState = PipelineState.RateLimited;
            }

            long? waitUntilTime = null;
            if (fields.TryGetValue("waitUntilTime", out string waitStr)
                && long.TryParse(waitStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out long waitVal))
                waitUntilTime = waitVal;

            int globalRetryCount = 0;
            if (fields.TryGetValue("globalRetryCount", out string grcStr)
                && int.TryParse(grcStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int grcVal))
                globalRetryCount = grcVal;

            var batchMetadata = new Dictionary<string, BatchMetadata>();
            int bmStart = json.IndexOf("\"batchMetadata\":{", StringComparison.Ordinal);
            if (bmStart >= 0)
            {
                int objStart = json.IndexOf('{', bmStart + 16);
                string bmJson = ExtractBalancedBraces(json, objStart);
                if (bmJson != null)
                    batchMetadata = ParseBatchMetadata(bmJson);
            }

            return new RetryState(pipelineState, waitUntilTime, globalRetryCount, batchMetadata);
        }

        private static Dictionary<string, string> ParseJsonFields(string json)
        {
            var result = new Dictionary<string, string>();
            int i = 0;
            while (i < json.Length)
            {
                int keyStart = json.IndexOf('"', i);
                if (keyStart < 0) break;
                int keyEnd = FindClosingQuote(json, keyStart);
                if (keyEnd < 0) break;
                string key = UnescapeJsonString(json.Substring(keyStart + 1, keyEnd - keyStart - 1));

                int colonIdx = json.IndexOf(':', keyEnd + 1);
                if (colonIdx < 0) break;

                int valStart = colonIdx + 1;
                while (valStart < json.Length && json[valStart] == ' ') valStart++;

                if (valStart >= json.Length) break;

                if (json[valStart] == '{')
                {
                    // Skip nested objects
                    i = SkipBraces(json, valStart) + 1;
                    continue;
                }

                if (json[valStart] == '"')
                {
                    int valEnd = FindClosingQuote(json, valStart);
                    if (valEnd < 0) break;
                    result[key] = UnescapeJsonString(json.Substring(valStart + 1, valEnd - valStart - 1));
                    i = valEnd + 1;
                }
                else
                {
                    int valEnd = valStart;
                    while (valEnd < json.Length && json[valEnd] != ',' && json[valEnd] != '}')
                        valEnd++;
                    result[key] = json.Substring(valStart, valEnd - valStart).Trim();
                    i = valEnd;
                }
            }
            return result;
        }

        private static int SkipBraces(string json, int start)
        {
            int depth = 0;
            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == '{') depth++;
                else if (json[i] == '}') { depth--; if (depth == 0) return i; }
            }
            return json.Length - 1;
        }

        private static string ExtractBalancedBraces(string json, int start)
        {
            if (start < 0 || start >= json.Length || json[start] != '{')
                return null;
            int end = SkipBraces(json, start);
            return json.Substring(start, end - start + 1);
        }

        private static Dictionary<string, BatchMetadata> ParseBatchMetadata(string json)
        {
            var result = new Dictionary<string, BatchMetadata>();
            int i = 1; // skip opening {
            while (i < json.Length)
            {
                int keyStart = json.IndexOf('"', i);
                if (keyStart < 0) break;
                int keyEnd = FindClosingQuote(json, keyStart);
                if (keyEnd < 0) break;
                string batchFile = UnescapeJsonString(json.Substring(keyStart + 1, keyEnd - keyStart - 1));

                int objStart = json.IndexOf('{', keyEnd + 1);
                if (objStart < 0) break;
                string metaJson = ExtractBalancedBraces(json, objStart);
                if (metaJson == null) break;

                var fields = ParseJsonFields(metaJson);

                int failureCount = 0;
                if (fields.TryGetValue("failureCount", out string fcStr))
                    int.TryParse(fcStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out failureCount);

                long? nextRetryTime = null;
                if (fields.TryGetValue("nextRetryTime", out string nrtStr)
                    && long.TryParse(nrtStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out long nrtVal))
                    nextRetryTime = nrtVal;

                long? firstFailureTime = null;
                if (fields.TryGetValue("firstFailureTime", out string fftStr)
                    && long.TryParse(fftStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out long fftVal))
                    firstFailureTime = fftVal;

                result[batchFile] = new BatchMetadata(failureCount, nextRetryTime, firstFailureTime);
                i = objStart + metaJson.Length + 1;
            }
            return result;
        }
    }
}
