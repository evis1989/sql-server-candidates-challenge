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
    public class GetProductsHandlerTests
    {
        private static readonly string[] Columns =
        {
            "ProductID", "Name", "ProductNumber", "Color", "StandardCost",
            "ListPrice", "Category", "Subcategory", "ModifiedDate"
        };

        [Test]
        public void CanHandle_only_GetProducts()
        {
            var handler = new GetProductsHandler(Mock.Of<IDbConnectionFactory>());

            Assert.That(handler.CanHandle(TaskTypes.GetProducts), Is.True);
            Assert.That(handler.CanHandle(TaskTypes.GetCustomers), Is.False);
            Assert.That(handler.CanHandle(TaskTypes.GetOrders), Is.False);
            Assert.That(handler.CanHandle(TaskTypes.GetProductInventory), Is.False);
        }

        [Test]
        public void Execute_maps_rows_with_decimals_and_date()
        {
            var modified = new DateTime(2025, 2, 7, 10, 1, 36, DateTimeKind.Utc);
            var rows = new[]
            {
                Row(680, "HL Road Frame - Black, 58", "FR-R92B-58", "Black",
                    1059.31m, 1431.50m, "Components", "Road Frames", modified),
                Row(706, "HL Road Frame - Red, 58", "FR-R92R-58", "Red",
                    1059.31m, 1431.50m, "Components", "Road Frames", modified)
            };
            var handler = new GetProductsHandler(FactoryReturning(rows, out _));

            var result = handler.Execute(NewTask());

            Assert.That(result.Status, Is.EqualTo("completed"));
            Assert.That(result.RecordCount, Is.EqualTo(2));

            var first = (ProductRecord)result.Data[0];
            Assert.That(first.ProductId, Is.EqualTo(680));
            Assert.That(first.Name, Is.EqualTo("HL Road Frame - Black, 58"));
            Assert.That(first.StandardCost, Is.EqualTo(1059.31m));
            Assert.That(first.ListPrice, Is.EqualTo(1431.50m));
            Assert.That(first.Category, Is.EqualTo("Components"));
            Assert.That(first.Subcategory, Is.EqualTo("Road Frames"));
            Assert.That(first.ModifiedDate, Is.EqualTo(modified));
        }

        [Test]
        public void Execute_tolerates_null_color_category_subcategory()
        {
            var rows = new[]
            {
                Row(1, "Adjustable Race", "AR-5381", null,
                    0m, 0m, null, null, new DateTime(2025, 8, 7, 0, 0, 0, DateTimeKind.Utc))
            };
            var handler = new GetProductsHandler(FactoryReturning(rows, out _));

            var result = handler.Execute(NewTask());

            Assert.That(result.Status, Is.EqualTo("completed"));
            var record = (ProductRecord)result.Data[0];
            Assert.That(record.Color, Is.Null);
            Assert.That(record.Category, Is.Null);
            Assert.That(record.Subcategory, Is.Null);
        }

        [Test]
        public void Execute_binds_modifiedSince_as_parameter()
        {
            var cutoff = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            IDbDataParameter captured;
            var handler = new GetProductsHandler(FactoryReturning(new Dictionary<string, object>[0], out captured));

            handler.Execute(NewTask(cutoff));

            Assert.That(captured.ParameterName, Is.EqualTo("@modifiedSince"));
            Assert.That(captured.Value, Is.EqualTo(cutoff));
        }

        [Test]
        public void Execute_returns_failed_when_connection_throws()
        {
            var factory = new Mock<IDbConnectionFactory>();
            factory.Setup(f => f.CreateConnection()).Throws(new InvalidOperationException("db down"));
            var handler = new GetProductsHandler(factory.Object);

            var result = handler.Execute(NewTask());

            Assert.That(result.Status, Is.EqualTo("failed"));
            Assert.That(result.Data, Is.Null);
            Assert.That(result.ErrorMessage, Is.EqualTo("db down"));
        }

        // --- helpers: build a mocked ADO.NET chain over in-memory rows ---

        private static SyncTask NewTask(DateTime? modifiedSince = null)
        {
            return new SyncTask
            {
                TaskId = "t1",
                TaskType = TaskTypes.GetProducts,
                Parameters = new TaskParameters
                {
                    ModifiedSince = modifiedSince ?? new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            };
        }

        private static Dictionary<string, object> Row(int productId, params object[] rest)
        {
            var row = new Dictionary<string, object> { ["ProductID"] = productId };
            for (var i = 1; i < Columns.Length; i++)
                row[Columns[i]] = rest[i - 1];
            return row;
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
            reader.Setup(r => r.GetOrdinal(It.IsAny<string>()))
                  .Returns<string>(name => Array.IndexOf(Columns, name));
            reader.Setup(r => r.IsDBNull(It.IsAny<int>()))
                  .Returns<int>(i => rows[index][Columns[i]] == null);
            reader.Setup(r => r.GetInt32(It.IsAny<int>()))
                  .Returns<int>(i => (int)rows[index][Columns[i]]);
            reader.Setup(r => r.GetString(It.IsAny<int>()))
                  .Returns<int>(i => (string)rows[index][Columns[i]]);
            reader.Setup(r => r.GetDecimal(It.IsAny<int>()))
                  .Returns<int>(i => (decimal)rows[index][Columns[i]]);
            reader.Setup(r => r.GetDateTime(It.IsAny<int>()))
                  .Returns<int>(i => (DateTime)rows[index][Columns[i]]);
            return reader.Object;
        }
    }
}
