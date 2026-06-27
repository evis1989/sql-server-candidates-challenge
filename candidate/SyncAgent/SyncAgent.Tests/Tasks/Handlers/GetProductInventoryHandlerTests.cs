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
    public class GetProductInventoryHandlerTests
    {
        private static readonly string[] Columns =
        {
            "ProductID", "ProductName", "ProductNumber", "LocationName",
            "Shelf", "Bin", "Quantity", "ModifiedDate"
        };

        [Test]
        public void CanHandle_only_GetProductInventory()
        {
            var handler = new GetProductInventoryHandler(Mock.Of<IDbConnectionFactory>());

            Assert.That(handler.CanHandle(TaskTypes.GetProductInventory), Is.True);
            Assert.That(handler.CanHandle(TaskTypes.GetCustomers), Is.False);
            Assert.That(handler.CanHandle(TaskTypes.GetProducts), Is.False);
            Assert.That(handler.CanHandle(TaskTypes.GetOrders), Is.False);
        }

        [Test]
        public void Execute_maps_rows_with_tinyint_bin_and_smallint_quantity()
        {
            var modified = new DateTime(2025, 8, 7, 0, 0, 0, DateTimeKind.Utc);
            var rows = new[]
            {
                Row(1, "Adjustable Race", "AR-5381", "Tool Crib", "A", (byte)1, (short)408, modified),
                Row(2, "Bearing Ball", "BA-8327", "Tool Crib", "A", (byte)2, (short)427, modified)
            };
            var handler = new GetProductInventoryHandler(FactoryReturning(rows, out _));

            var result = handler.Execute(NewTask());

            Assert.That(result.Status, Is.EqualTo("completed"));
            Assert.That(result.RecordCount, Is.EqualTo(2));

            var first = (InventoryRecord)result.Data[0];
            Assert.That(first.ProductId, Is.EqualTo(1));
            Assert.That(first.ProductName, Is.EqualTo("Adjustable Race"));
            Assert.That(first.LocationName, Is.EqualTo("Tool Crib"));
            Assert.That(first.Shelf, Is.EqualTo("A"));
            Assert.That(first.Bin, Is.EqualTo(1));
            Assert.That(first.Quantity, Is.EqualTo(408));
            Assert.That(first.ModifiedDate, Is.EqualTo(modified));

            var second = (InventoryRecord)result.Data[1];
            Assert.That(second.Quantity, Is.EqualTo(427));
        }

        [Test]
        public void Execute_binds_modifiedSince_as_parameter()
        {
            var cutoff = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            IDbDataParameter captured;
            var handler = new GetProductInventoryHandler(FactoryReturning(new Dictionary<string, object>[0], out captured));

            handler.Execute(NewTask(cutoff));

            Assert.That(captured.ParameterName, Is.EqualTo("@modifiedSince"));
            Assert.That(captured.Value, Is.EqualTo(cutoff));
        }

        [Test]
        public void Execute_returns_failed_when_connection_throws()
        {
            var factory = new Mock<IDbConnectionFactory>();
            factory.Setup(f => f.CreateConnection()).Throws(new InvalidOperationException("db down"));
            var handler = new GetProductInventoryHandler(factory.Object);

            var result = handler.Execute(NewTask());

            Assert.That(result.Status, Is.EqualTo("failed"));
            Assert.That(result.Data, Is.Null);
            Assert.That(result.ErrorMessage, Is.EqualTo("db down"));
        }

        // --- helpers ---

        private static SyncTask NewTask(DateTime? modifiedSince = null)
        {
            return new SyncTask
            {
                TaskId = "t1",
                TaskType = TaskTypes.GetProductInventory,
                Parameters = new TaskParameters
                {
                    ModifiedSince = modifiedSince ?? new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            };
        }

        private static Dictionary<string, object> Row(
            int productId, string name, string number, string location, string shelf, object bin, object quantity, DateTime modified)
        {
            return new Dictionary<string, object>
            {
                ["ProductID"] = productId,
                ["ProductName"] = name,
                ["ProductNumber"] = number,
                ["LocationName"] = location,
                ["Shelf"] = shelf,
                ["Bin"] = bin,
                ["Quantity"] = quantity,
                ["ModifiedDate"] = modified
            };
        }

        private static IDbConnectionFactory FactoryReturning(
            IReadOnlyList<Dictionary<string, object>> rows, out IDbDataParameter captured)
        {
            var reader = BuildReader(rows);

            var parameter = new Mock<IDbDataParameter>();
            parameter.SetupAllProperties();
            captured = parameter.Object;

            var command = new Mock<IDbCommand>();
            command.SetupAllProperties();
            command.Setup(c => c.CreateParameter()).Returns(parameter.Object);
            command.Setup(c => c.Parameters).Returns(Mock.Of<IDataParameterCollection>());
            command.Setup(c => c.ExecuteReader()).Returns(reader);

            var connection = new Mock<IDbConnection>();
            connection.Setup(c => c.CreateCommand()).Returns(command.Object);

            var factory = new Mock<IDbConnectionFactory>();
            factory.Setup(f => f.CreateConnection()).Returns(connection.Object);
            return factory.Object;
        }

        private static IDataReader BuildReader(IReadOnlyList<Dictionary<string, object>> rows)
        {
            var index = -1;
            var reader = new Mock<IDataReader>();
            reader.Setup(r => r.Read()).Returns(() => ++index < rows.Count);
            reader.Setup(r => r.GetOrdinal(It.IsAny<string>())).Returns<string>(name => Array.IndexOf(Columns, name));
            reader.Setup(r => r.IsDBNull(It.IsAny<int>())).Returns<int>(i => rows[index][Columns[i]] == null);
            reader.Setup(r => r.GetInt32(It.IsAny<int>())).Returns<int>(i => (int)rows[index][Columns[i]]);
            reader.Setup(r => r.GetString(It.IsAny<int>())).Returns<int>(i => (string)rows[index][Columns[i]]);
            reader.Setup(r => r.GetDateTime(It.IsAny<int>())).Returns<int>(i => (DateTime)rows[index][Columns[i]]);
            reader.Setup(r => r.GetValue(It.IsAny<int>())).Returns<int>(i => rows[index][Columns[i]]);
            return reader.Object;
        }
    }
}
