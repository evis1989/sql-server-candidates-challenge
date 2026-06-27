using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SyncAgent.Models
{
    /// <summary>Result payload sent to POST /api/sync/result.</summary>
    public class SyncResult
    {
        [JsonProperty("taskId")]
        public string TaskId { get; set; }

        [JsonProperty("taskType")]
        public string TaskType { get; set; }

        /// <summary>"completed" or "failed".</summary>
        [JsonProperty("status")]
        public string Status { get; set; }

        /// <summary>Query results, or null when the task failed.</summary>
        [JsonProperty("data")]
        public IReadOnlyList<object> Data { get; set; }

        [JsonProperty("recordCount")]
        public int RecordCount { get; set; }

        [JsonProperty("executedAt")]
        public DateTime ExecutedAt { get; set; }

        /// <summary>Error description when failed; always serialized, null when there is no error.</summary>
        [JsonProperty("errorMessage", NullValueHandling = NullValueHandling.Include)]
        public string ErrorMessage { get; set; }

        /// <summary>Builds a completed result for the given data.</summary>
        public static SyncResult Completed(string taskId, string taskType, IReadOnlyList<object> data)
        {
            return new SyncResult
            {
                TaskId = taskId,
                TaskType = taskType,
                Status = "completed",
                Data = data,
                RecordCount = data?.Count ?? 0,
                ExecutedAt = DateTime.UtcNow,
                ErrorMessage = null
            };
        }

        /// <summary>Builds a failed result carrying the error message.</summary>
        public static SyncResult Failed(string taskId, string taskType, string errorMessage)
        {
            return new SyncResult
            {
                TaskId = taskId,
                TaskType = taskType,
                Status = "failed",
                Data = null,
                RecordCount = 0,
                ExecutedAt = DateTime.UtcNow,
                ErrorMessage = errorMessage
            };
        }
    }
}
