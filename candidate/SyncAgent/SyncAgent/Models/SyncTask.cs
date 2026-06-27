using System;
using Newtonsoft.Json;

namespace SyncAgent.Models
{
    /// <summary>A sync task retrieved from GET /api/sync/next-task.</summary>
    public class SyncTask
    {
        [JsonProperty("taskId")]
        public string TaskId { get; set; }

        [JsonProperty("taskType")]
        public string TaskType { get; set; }

        [JsonProperty("parameters")]
        public TaskParameters Parameters { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }
    }
}
