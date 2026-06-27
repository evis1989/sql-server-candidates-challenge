using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using SyncAgent.Database;
using SyncAgent.Models;

namespace SyncAgent.Tasks.Handlers
{
    /// <summary>
    /// Executes GetOrders tasks. An order carries a nested orderDetails[], so it is
    /// fetched as two queries — headers, then details for those header ids — and
    /// stitched in memory, rather than de-duplicating a header×detail JOIN.
    /// </summary>
    public class GetOrdersHandler : ITaskHandler
    {
        private const string HeaderSql = @"
SELECT
    soh.SalesOrderID,
    soh.OrderDate,
    soh.Status,
    p.FirstName,
    p.LastName,
    c.AccountNumber,
    soh.TotalDue
FROM Sales.SalesOrderHeader soh
INNER JOIN Sales.Customer c
    ON c.CustomerID = soh.CustomerID
LEFT JOIN Person.Person p
    ON p.BusinessEntityID = c.PersonID
WHERE soh.ModifiedDate >= @modifiedSince
ORDER BY soh.SalesOrderID;";

        private const string DetailSqlHead = @"
SELECT
    sod.SalesOrderID,
    prod.Name AS ProductName,
    prod.ProductNumber,
    sod.UnitPrice,
    sod.OrderQty,
    sod.LineTotal
FROM Sales.SalesOrderDetail sod
INNER JOIN Production.Product prod
    ON prod.ProductID = sod.ProductID
WHERE sod.SalesOrderID IN (";

        private const string DetailSqlTail = @")
ORDER BY sod.SalesOrderID, sod.SalesOrderDetailID;";

        private readonly IDbConnectionFactory _connectionFactory;

        public GetOrdersHandler(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public bool CanHandle(string taskType)
        {
            return taskType == TaskTypes.GetOrders;
        }

        public SyncResult Execute(SyncTask task)
        {
            try
            {
                using (var connection = _connectionFactory.CreateConnection())
                {
                    connection.Open();

                    var orders = DbQuery.ReadRows(
                        connection,
                        HeaderSql,
                        command => DbQuery.AddParameter(command, "@modifiedSince", task.Parameters.ModifiedSince),
                        MapHeader).Cast<OrderRecord>().ToList();

                    // Zero headers: skip the detail query entirely — IN () is invalid SQL.
                    if (orders.Count > 0)
                        AttachDetails(connection, orders);

                    return SyncResult.Completed(task.TaskId, task.TaskType, orders);
                }
            }
            catch (Exception ex)
            {
                return SyncResult.Failed(task.TaskId, task.TaskType, ex.Message);
            }
        }

        private static void AttachDetails(IDbConnection connection, IReadOnlyList<OrderRecord> orders)
        {
            var byId = orders.ToDictionary(o => o.SalesOrderId);

            var details = DbQuery.ReadRows(
                connection,
                BuildDetailSql(orders.Count),
                command => BindIds(command, orders),
                MapDetail);

            foreach (DetailRow row in details)
            {
                if (byId.TryGetValue(row.OrderId, out var order))
                    order.OrderDetails.Add(row.Detail);
            }
        }

        // Generates "@id0, @id1, ..." — parameter NAMES into the SQL text (safe);
        // the id VALUES are bound as parameters in BindIds (never concatenated).
        private static string BuildDetailSql(int idCount)
        {
            var placeholders = string.Join(", ", Enumerable.Range(0, idCount).Select(i => "@id" + i));
            return DetailSqlHead + placeholders + DetailSqlTail;
        }

        private static void BindIds(IDbCommand command, IReadOnlyList<OrderRecord> orders)
        {
            for (var i = 0; i < orders.Count; i++)
                DbQuery.AddParameter(command, "@id" + i, orders[i].SalesOrderId);
        }

        private static object MapHeader(IDataReader reader)
        {
            return new OrderRecord
            {
                SalesOrderId = reader.GetInt32(reader.GetOrdinal("SalesOrderID")),
                OrderDate = reader.GetDateTime(reader.GetOrdinal("OrderDate")),
                // Status is tinyint, OrderQty is smallint — read type-agnostically.
                Status = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("Status"))),
                CustomerName = ComposeName(
                    DbQuery.GetStringOrNull(reader, "FirstName"),
                    DbQuery.GetStringOrNull(reader, "LastName")),
                AccountNumber = DbQuery.GetStringOrNull(reader, "AccountNumber"),
                TotalDue = reader.GetDecimal(reader.GetOrdinal("TotalDue"))
            };
        }

        private static object MapDetail(IDataReader reader)
        {
            return new DetailRow
            {
                OrderId = reader.GetInt32(reader.GetOrdinal("SalesOrderID")),
                Detail = new OrderDetailRecord
                {
                    ProductName = DbQuery.GetStringOrNull(reader, "ProductName"),
                    ProductNumber = DbQuery.GetStringOrNull(reader, "ProductNumber"),
                    UnitPrice = reader.GetDecimal(reader.GetOrdinal("UnitPrice")),
                    Quantity = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("OrderQty"))),
                    LineTotal = reader.GetDecimal(reader.GetOrdinal("LineTotal"))
                }
            };
        }

        private static string ComposeName(string first, string last)
        {
            var name = ((first ?? "") + " " + (last ?? "")).Trim();
            return name.Length == 0 ? null : name;
        }

        /// <summary>Carries a detail row plus the order id it belongs to, for stitching.</summary>
        private sealed class DetailRow
        {
            public int OrderId;
            public OrderDetailRecord Detail;
        }
    }
}
