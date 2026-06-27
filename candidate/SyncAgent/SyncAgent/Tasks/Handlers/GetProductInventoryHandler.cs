using System;
using System.Data;
using SyncAgent.Database;
using SyncAgent.Models;

namespace SyncAgent.Tasks.Handlers
{
    /// <summary>
    /// Executes GetProductInventory tasks: queries AdventureWorks2025 for inventory
    /// modified since the task's cut-off, one flat row per product-location record.
    /// </summary>
    public class GetProductInventoryHandler : SqlTaskHandler
    {
        private const string InventorySql = @"
SELECT
    pi.ProductID,
    p.Name AS ProductName,
    p.ProductNumber,
    l.Name AS LocationName,
    pi.Shelf,
    pi.Bin,
    pi.Quantity,
    pi.ModifiedDate
FROM Production.ProductInventory pi
INNER JOIN Production.Product p
    ON p.ProductID = pi.ProductID
INNER JOIN Production.Location l
    ON l.LocationID = pi.LocationID
WHERE pi.ModifiedDate >= @modifiedSince
ORDER BY pi.ProductID, l.LocationID;";

        public GetProductInventoryHandler(IDbConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        protected override string TaskType => TaskTypes.GetProductInventory;

        protected override string Sql => InventorySql;

        protected override object MapRow(IDataReader reader)
        {
            return new InventoryRecord
            {
                ProductId = reader.GetInt32(reader.GetOrdinal("ProductID")),
                ProductName = GetStringOrNull(reader, "ProductName"),
                ProductNumber = GetStringOrNull(reader, "ProductNumber"),
                LocationName = GetStringOrNull(reader, "LocationName"),
                Shelf = GetStringOrNull(reader, "Shelf"),
                // Bin is tinyint, Quantity is smallint — read type-agnostically.
                Bin = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("Bin"))),
                Quantity = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("Quantity"))),
                ModifiedDate = reader.GetDateTime(reader.GetOrdinal("ModifiedDate"))
            };
        }
    }
}
