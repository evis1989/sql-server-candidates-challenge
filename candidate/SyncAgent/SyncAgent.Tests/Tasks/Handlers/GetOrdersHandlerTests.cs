using System;
using System.Collections.Generic;
using System.Data;
using Moq;
using NUnit.Framework;
using SyncAgent.Database;
using SyncAgent.Models;
using SyncAgent.Tasks;
using SyncAgent.Tasks.Handlers;

namespace SyncAgent.Tests.Tasks.Handlers
{
    [TestFixture]
    public class GetOrdersHandlerTests
    {
        private static readonly string[] HeaderCols =
        {
            "SalesOrderID", "OrderDate", "Status", "FirstName", "LastName", "AccountNumber", "TotalDue"
        };

        private static readonly string[] DetailCols =
        {
            "SalesOrderID", "ProductName", "ProductNumber", "UnitPrice", "OrderQty", "LineTotal"
        };

        [Test]
        public void CanHandle_only_GetOrders()
        {
            var handler = new GetOrdersHandler(Mock.Of<IDbConnectionFactory>());

            Assert.That(handler.CanHandle(TaskTypes.GetOrders), Is.True);
            Assert.That(handler.CanHandle(TaskTypes.GetCustomers), Is.False);
            Assert.That(handler.CanHandle(TaskTypes.GetProducts), Is.False);
            Assert.That(handler.CanHandle(TaskTypes.GetProductInventory), Is.False);
        }

        [Test]
        public void Execute_stitches_details_under_their_order()
        {
            var date = new DateTime(2022, 5, 30, 0, 0, 0, DateTimeKind.Utc);
            var headers = new[]
            {
                HeaderRow(43659, date, (byte)5, "James", "Hendergart", "AW00029825", 23153.2339m),
                HeaderRow(43660, date, (byte)5, "Cora", "Reed", "AW00029827", 1457.3288m)
            };
            var details = new[]
            {
                DetailRow(43659, "Mountain-100 Black, 42", "BK-M82B-42", 2024.994m, (short)1, 2024.994m),
                DetailRow(43659, "Sport-100 Helmet, Blue", "HL-U509-B", 20.1865m, (short)4, 80.746m),
                DetailRow(43660, "Road-150 Red, 62", "BK-R93R-62", 1457.3288m, (short)1, 1457.3288m)
            };
            var handler = new GetOrdersHandler(Factory(headers, details, out _, out _));

            var result = handler.Execute(NewTask());

            Assert.That(result.Status, Is.EqualTo("completed"));
            Assert.That(result.RecordCount, Is.EqualTo(2), "recordCount counts orders, not detail lines");

            var first = (OrderRecord)result.Data[0];
            Assert.That(first.SalesOrderId, Is.EqualTo(43659));
            Assert.That(first.CustomerName, Is.EqualTo("James Hendergart"));
            Assert.That(first.Status, Is.EqualTo(5));
            Assert.That(first.TotalDue, Is.EqualTo(23153.2339m));
            Assert.That(first.OrderDetails.Count, Is.EqualTo(2));
            Assert.That(first.OrderDetails[0].ProductNumber, Is.EqualTo("BK-M82B-42"));
            Assert.That(first.OrderDetails[0].Quantity, Is.EqualTo(1));
            Assert.That(first.OrderDetails[1].LineTotal, Is.EqualTo(80.746m));

            var second = (OrderRecord)result.Data[1];
            Assert.That(second.OrderDetails.Count, Is.EqualTo(1));
            Assert.That(second.OrderDetails[0].ProductNumber, Is.EqualTo("BK-R93R-62"));
        }

        [Test]
        public void Execute_with_no_headers_skips_detail_query()
        {
            var connectionCreateCommandCalls = 0;
            var handler = new GetOrdersHandler(
                Factory(new Dictionary<string, object>[0], new Dictionary<string, object>[0],
                    out _, out _, () => connectionCreateCommandCalls++));

            var result = handler.Execute(NewTask());

            Assert.That(result.Status, Is.EqualTo("completed"));
            Assert.That(result.RecordCount, Is.EqualTo(0));
            Assert.That(result.Data, Is.Empty);
            Assert.That(connectionCreateCommandCalls, Is.EqualTo(1), "detail query must not run when there are no headers");
        }

        [Test]
        public void Execute_binds_modifiedSince_and_id_parameters()
        {
            var cutoff = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var date = new DateTime(2022, 5, 30, 0, 0, 0, DateTimeKind.Utc);
            var headers = new[]
            {
                HeaderRow(43659, date, (byte)5, "James", "Hendergart", "AW00029825", 1m),
                HeaderRow(43660, date, (byte)5, "Cora", "Reed", "AW00029827", 1m)
            };
            List<IDbDataParameter> headerParams, detailParams;
            var handler = new GetOrdersHandler(
                Factory(headers, new Dictionary<string, object>[0], out headerParams, out detailParams));

            handler.Execute(NewTask(cutoff));

            Assert.That(headerParams, Has.Count.EqualTo(1));
            Assert.That(headerParams[0].ParameterName, Is.EqualTo("@modifiedSince"));
            Assert.That(headerParams[0].Value, Is.EqualTo(cutoff));

            Assert.That(detailParams, Has.Count.EqualTo(2));
            Assert.That(detailParams[0].ParameterName, Is.EqualTo("@id0"));
            Assert.That(detailParams[0].Value, Is.EqualTo(43659));
            Assert.That(detailParams[1].ParameterName, Is.EqualTo("@id1"));
            Assert.That(detailParams[1].Value, Is.EqualTo(43660));
        }

        [Test]
        public void Execute_returns_failed_when_connection_throws()
        {
            var factory = new Mock<IDbConnectionFactory>();
            factory.Setup(f => f.CreateConnection()).Throws(new InvalidOperationException("db down"));
            var handler = new GetOrdersHandler(factory.Object);

            var result = handler.Execute(NewTask());

            Assert.That(result.Status, Is.EqualTo("failed"));
            Assert.That(result.Data, Is.Null);
            Assert.That(result.ErrorMessage, Is.EqualTo("db down"));
        }

        [Test]
        public void Execute_batches_detail_query_to_stay_under_the_parameter_cap()
        {
            var date = new DateTime(2022, 5, 30, 0, 0, 0, DateTimeKind.Utc);
            var headers = new[]
            {
                HeaderRow(1, date, (byte)5, "A", "A", "AW1", 1m),
                HeaderRow(2, date, (byte)5, "B", "B", "AW2", 1m),
                HeaderRow(3, date, (byte)5, "C", "C", "AW3", 1m)
            };
            var batch1 = new[]
            {
                DetailRow(1, "P1", "PN1", 1m, (short)1, 1m),
                DetailRow(2, "P2", "PN2", 1m, (short)1, 1m)
            };
            var batch2 = new[] { DetailRow(3, "P3", "PN3", 1m, (short)1, 1m) };

            var detailParamBatches = new List<List<IDbDataParameter>>();
            var factory = FactoryWithReaders(
                BuildReader(HeaderCols, headers),
                new[] { BuildReader(DetailCols, batch1), BuildReader(DetailCols, batch2) },
                detailParamBatches);
            // batch size 2 → 3 orders split into batches of [1,2] and [3]
            var handler = new GetOrdersHandler(factory, 2);

            var result = handler.Execute(NewTask());

            Assert.That(result.RecordCount, Is.EqualTo(3));
            var orders = new List<OrderRecord>();
            foreach (var o in result.Data) orders.Add((OrderRecord)o);
            Assert.That(orders[0].OrderDetails[0].ProductNumber, Is.EqualTo("PN1"));
            Assert.That(orders[2].OrderDetails[0].ProductNumber, Is.EqualTo("PN3"));

            Assert.That(detailParamBatches, Has.Count.EqualTo(2), "two detail queries, one per batch");
            Assert.That(detailParamBatches[0], Has.Count.EqualTo(2), "first batch binds 2 ids");
            Assert.That(detailParamBatches[1], Has.Count.EqualTo(1), "second batch binds 1 id");
        }

        // --- helpers ---

        private static SyncTask NewTask(DateTime? modifiedSince = null)
        {
            return new SyncTask
            {
                TaskId = "t1",
                TaskType = TaskTypes.GetOrders,
                Parameters = new TaskParameters
                {
                    ModifiedSince = modifiedSince ?? new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            };
        }

        private static Dictionary<string, object> HeaderRow(
            int id, DateTime orderDate, object status, string first, string last, string account, decimal totalDue)
        {
            return new Dictionary<string, object>
            {
                ["SalesOrderID"] = id,
                ["OrderDate"] = orderDate,
                ["Status"] = status,
                ["FirstName"] = first,
                ["LastName"] = last,
                ["AccountNumber"] = account,
                ["TotalDue"] = totalDue
            };
        }

        private static Dictionary<string, object> DetailRow(
            int orderId, string name, string number, decimal unitPrice, object qty, decimal lineTotal)
        {
            return new Dictionary<string, object>
            {
                ["SalesOrderID"] = orderId,
                ["ProductName"] = name,
                ["ProductNumber"] = number,
                ["UnitPrice"] = unitPrice,
                ["OrderQty"] = qty,
                ["LineTotal"] = lineTotal
            };
        }

        private static IDbConnectionFactory Factory(
            IReadOnlyList<Dictionary<string, object>> headers,
            IReadOnlyList<Dictionary<string, object>> details,
            out List<IDbDataParameter> headerParams,
            out List<IDbDataParameter> detailParams,
            Action onCreateCommand = null)
        {
            headerParams = new List<IDbDataParameter>();
            detailParams = new List<IDbDataParameter>();

            var headerCommand = BuildCommand(BuildReader(HeaderCols, headers), headerParams);
            var detailCommand = BuildCommand(BuildReader(DetailCols, details), detailParams);

            var connection = new Mock<IDbConnection>();
            // Return the header command on the first CreateCommand call, the detail command on the second.
            connection.Setup(c => c.CreateCommand()).Returns(NextCommand(headerCommand, detailCommand, onCreateCommand));

            var factory = new Mock<IDbConnectionFactory>();
            factory.Setup(f => f.CreateConnection()).Returns(connection.Object);
            return factory.Object;
        }

        // Header command first, then one detail command per batch; each detail batch's
        // bound parameters are captured into detailParamBatches in order.
        private static IDbConnectionFactory FactoryWithReaders(
            IDataReader headerReader,
            IReadOnlyList<IDataReader> detailReaders,
            List<List<IDbDataParameter>> detailParamBatches)
        {
            var commands = new Queue<IDbCommand>();
            commands.Enqueue(BuildCommand(headerReader, new List<IDbDataParameter>()));
            foreach (var reader in detailReaders)
            {
                var captured = new List<IDbDataParameter>();
                detailParamBatches.Add(captured);
                commands.Enqueue(BuildCommand(reader, captured));
            }

            var connection = new Mock<IDbConnection>();
            connection.Setup(c => c.CreateCommand()).Returns(() => commands.Dequeue());

            var factory = new Mock<IDbConnectionFactory>();
            factory.Setup(f => f.CreateConnection()).Returns(connection.Object);
            return factory.Object;
        }

        private static Func<IDbCommand> NextCommand(IDbCommand header, IDbCommand detail, Action onCreate)
        {
            var calls = 0;
            return () =>
            {
                onCreate?.Invoke();
                return calls++ == 0 ? header : detail;
            };
        }

        private static IDbCommand BuildCommand(IDataReader reader, List<IDbDataParameter> captured)
        {
            var command = new Mock<IDbCommand>();
            command.SetupAllProperties();
            command.Setup(c => c.CreateParameter()).Returns(() =>
            {
                var p = new Mock<IDbDataParameter>();
                p.SetupAllProperties();
                return p.Object;
            });
            var parameters = new Mock<IDataParameterCollection>();
            parameters.Setup(pc => pc.Add(It.IsAny<object>()))
                      .Returns<object>(o => { captured.Add((IDbDataParameter)o); return captured.Count - 1; });
            command.Setup(c => c.Parameters).Returns(parameters.Object);
            command.Setup(c => c.ExecuteReader()).Returns(reader);
            return command.Object;
        }

        private static IDataReader BuildReader(string[] cols, IReadOnlyList<Dictionary<string, object>> rows)
        {
            var index = -1;
            var reader = new Mock<IDataReader>();
            reader.Setup(r => r.Read()).Returns(() => ++index < rows.Count);
            reader.Setup(r => r.GetOrdinal(It.IsAny<string>())).Returns<string>(name => Array.IndexOf(cols, name));
            reader.Setup(r => r.IsDBNull(It.IsAny<int>())).Returns<int>(i => rows[index][cols[i]] == null);
            reader.Setup(r => r.GetInt32(It.IsAny<int>())).Returns<int>(i => (int)rows[index][cols[i]]);
            reader.Setup(r => r.GetString(It.IsAny<int>())).Returns<int>(i => (string)rows[index][cols[i]]);
            reader.Setup(r => r.GetDecimal(It.IsAny<int>())).Returns<int>(i => (decimal)rows[index][cols[i]]);
            reader.Setup(r => r.GetDateTime(It.IsAny<int>())).Returns<int>(i => (DateTime)rows[index][cols[i]]);
            reader.Setup(r => r.GetValue(It.IsAny<int>())).Returns<int>(i => rows[index][cols[i]]);
            return reader.Object;
        }
    }
}
