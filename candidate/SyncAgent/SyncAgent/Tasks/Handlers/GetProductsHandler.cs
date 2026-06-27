using System.Data;
using SyncAgent.Database;
using SyncAgent.Models;

namespace SyncAgent.Tasks.Handlers
{
    /// <summary>
    /// Executes GetProducts tasks: queries AdventureWorks2025 for products modified
    /// since the task's cut-off, with category/subcategory, mapped to the contract shape.
    /// </summary>
    public class GetProductsHandler : SqlTaskHandler
    {
        // LEFT joins to subcategory/category: a product may have no subcategory
        // (and therefore no category), in which case both come back null.
        private const string ProductsSql = @"
SELECT
    p.ProductID,
    p.Name,
    p.ProductNumber,
    p.Color,
    p.StandardCost,
    p.ListPrice,
    pc.Name AS Category,
    ps.Name AS Subcategory,
    p.ModifiedDate
FROM Production.Product p
LEFT JOIN Production.ProductSubcategory ps
    ON ps.ProductSubcategoryID = p.ProductSubcategoryID
LEFT JOIN Production.ProductCategory pc
    ON pc.ProductCategoryID = ps.ProductCategoryID
WHERE p.ModifiedDate >= @modifiedSince
ORDER BY p.ProductID;";

        public GetProductsHandler(IDbConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        protected override string TaskType => TaskTypes.GetProducts;

        protected override string Sql => ProductsSql;

        protected override object MapRow(IDataReader reader)
        {
            return new ProductRecord
            {
                ProductId = reader.GetInt32(reader.GetOrdinal("ProductID")),
                Name = GetStringOrNull(reader, "Name"),
                ProductNumber = GetStringOrNull(reader, "ProductNumber"),
                Color = GetStringOrNull(reader, "Color"),
                StandardCost = reader.GetDecimal(reader.GetOrdinal("StandardCost")),
                ListPrice = reader.GetDecimal(reader.GetOrdinal("ListPrice")),
                Category = GetStringOrNull(reader, "Category"),
                Subcategory = GetStringOrNull(reader, "Subcategory"),
                ModifiedDate = reader.GetDateTime(reader.GetOrdinal("ModifiedDate"))
            };
        }
    }
}
