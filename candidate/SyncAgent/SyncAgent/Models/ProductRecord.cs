using System;
using Newtonsoft.Json;

namespace SyncAgent.Models
{
    /// <summary>One product row returned by a GetProducts task.</summary>
    public class ProductRecord
    {
        [JsonProperty("productId")]
        public int ProductId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("productNumber")]
        public string ProductNumber { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("standardCost")]
        public decimal StandardCost { get; set; }

        [JsonProperty("listPrice")]
        public decimal ListPrice { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("subcategory")]
        public string Subcategory { get; set; }

        [JsonProperty("modifiedDate")]
        public DateTime ModifiedDate { get; set; }
    }
}
