using System;
using System.Globalization;
using System.Collections.Generic;
using Segment.Analytics.Utilities;
using Segment.Serialization;

namespace Segment.Analytics.Retry
{
    internal static class RetryStateStorage
    {
        public static void SaveRetryState(IStorage storage, RetryState state)
        {
            try
            {
                string json = JsonUtility.ToJson(Serialize(state));
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
                return Deserialize(JsonUtility.FromJson<JsonObject>(json));
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

        private static JsonObject Serialize(RetryState state)
        {
            // PipelineState is written by name (order-independent); numbers are written
            // as real JSON numbers so they round-trip without precision loss.
            var root = new JsonObject
            {
                ["pipelineState"] = state.PipelineState.ToString(),
                ["globalRetryCount"] = state.GlobalRetryCount,
            };
            if (state.WaitUntilTime.HasValue)
                root["waitUntilTime"] = state.WaitUntilTime.Value;

            if (state.BatchMetadata.Count > 0)
            {
                var batchMetadata = new JsonObject();
                foreach (KeyValuePair<string, BatchMetadata> kvp in state.BatchMetadata)
                {
                    var meta = new JsonObject { ["failureCount"] = kvp.Value.FailureCount };
                    if (kvp.Value.NextRetryTime.HasValue)
                        meta["nextRetryTime"] = kvp.Value.NextRetryTime.Value;
                    if (kvp.Value.FirstFailureTime.HasValue)
                        meta["firstFailureTime"] = kvp.Value.FirstFailureTime.Value;
                    batchMetadata[kvp.Key] = meta;
                }
                root["batchMetadata"] = batchMetadata;
            }

            return root;
        }

        private static RetryState Deserialize(JsonObject root)
        {
            // Numbers are read via GetString + TryParse rather than GetLong/GetInt to
            // avoid Serialization.NET coercing large (epoch-millis) longs through float.
            PipelineState pipelineState = PipelineState.Ready;
            string psVal = root.GetString("pipelineState", null);
            // Written as the enum name. "1" is also accepted as a defensive net for the
            // never-released ordinal form (it's a string, so no numeric coercion).
            if (psVal == PipelineState.RateLimited.ToString() || psVal == "1")
                pipelineState = PipelineState.RateLimited;

            long? waitUntilTime = ReadNullableLong(root, "waitUntilTime");
            int globalRetryCount = ReadInt(root, "globalRetryCount");

            var batchMetadata = new Dictionary<string, BatchMetadata>();
            JsonObject batchMetadataJson = root.GetJsonObject("batchMetadata", null);
            if (batchMetadataJson != null)
            {
                foreach (string batchFile in batchMetadataJson.Keys)
                {
                    JsonObject meta = batchMetadataJson.GetJsonObject(batchFile, null);
                    if (meta == null)
                        continue;
                    batchMetadata[batchFile] = new BatchMetadata(
                        failureCount: ReadInt(meta, "failureCount"),
                        nextRetryTime: ReadNullableLong(meta, "nextRetryTime"),
                        firstFailureTime: ReadNullableLong(meta, "firstFailureTime"));
                }
            }

            return new RetryState(pipelineState, waitUntilTime, globalRetryCount, batchMetadata);
        }

        private static int ReadInt(JsonObject json, string key)
        {
            string s = json.GetString(key, null);
            return s != null && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
                ? v : 0;
        }

        private static long? ReadNullableLong(JsonObject json, string key)
        {
            string s = json.GetString(key, null);
            return s != null && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v)
                ? v : (long?)null;
        }
    }
}
