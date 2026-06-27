using System;
using Newtonsoft.Json;

namespace SyncAgent.Models
{
    /// <summary>One product-location inventory record returned by a GetProductInventory task.</summary>
    public class InventoryRecord
    {
        [JsonProperty("productId")]
        public int ProductId { get; set; }

        [JsonProperty("productName")]
        public string ProductName { get; set; }

        [JsonProperty("productNumber")]
        public string ProductNumber { get; set; }

        [JsonProperty("locationName")]
        public string LocationName { get; set; }

        [JsonProperty("shelf")]
        public string Shelf { get; set; }

        [JsonProperty("bin")]
        public int Bin { get; set; }

        [JsonProperty("quantity")]
        public int Quantity { get; set; }

        [JsonProperty("modifiedDate")]
        public DateTime ModifiedDate { get; set; }
    }
}
