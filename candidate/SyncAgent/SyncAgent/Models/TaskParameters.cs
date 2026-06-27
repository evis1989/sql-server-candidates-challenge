using System;
using Newtonsoft.Json;

namespace SyncAgent.Models
{
    /// <summary>Parameters carried by a sync task.</summary>
    public class TaskParameters
    {
        /// <summary>Only records modified on or after this instant are returned.</summary>
        [JsonProperty("modifiedSince")]
        public DateTime ModifiedSince { get; set; }
    }
}
