using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SyncAgent.Models
{
    /// <summary>One order (header plus its line items) returned by a GetOrders task.</summary>
    public class OrderRecord
    {
        [JsonProperty("salesOrderId")]
        public int SalesOrderId { get; set; }

        [JsonProperty("orderDate")]
        public DateTime OrderDate { get; set; }

        [JsonProperty("status")]
        public int Status { get; set; }

        [JsonProperty("customerName")]
        public string CustomerName { get; set; }

        [JsonProperty("accountNumber")]
        public string AccountNumber { get; set; }

        [JsonProperty("totalDue")]
        public decimal TotalDue { get; set; }

        [JsonProperty("orderDetails")]
        public List<OrderDetailRecord> OrderDetails { get; set; } = new List<OrderDetailRecord>();
    }
}
