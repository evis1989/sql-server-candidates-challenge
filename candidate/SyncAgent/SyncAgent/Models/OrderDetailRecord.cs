using Newtonsoft.Json;

namespace SyncAgent.Models
{
    /// <summary>One line item within an order returned by a GetOrders task.</summary>
    public class OrderDetailRecord
    {
        [JsonProperty("productName")]
        public string ProductName { get; set; }

        [JsonProperty("productNumber")]
        public string ProductNumber { get; set; }

        [JsonProperty("unitPrice")]
        public decimal UnitPrice { get; set; }

        [JsonProperty("quantity")]
        public int Quantity { get; set; }

        [JsonProperty("lineTotal")]
        public decimal LineTotal { get; set; }
    }
}
