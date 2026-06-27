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
    public class GetCustomersHandlerTests
    {
        private static readonly string[] Columns =
        {
            "CustomerID", "AccountNumber", "FirstName", "LastName", "EmailAddress",
            "Phone", "AddressLine1", "City", "StateProvince", "PostalCode", "CountryRegion"
        };

        [Test]
        public void CanHandle_only_GetCustomers()
        {
            var handler = new GetCustomersHandler(Mock.Of<IDbConnectionFactory>());

            Assert.That(handler.CanHandle(TaskTypes.GetCustomers), Is.True);
            Assert.That(handler.CanHandle(TaskTypes.GetProducts), Is.False);
            Assert.That(handler.CanHandle(TaskTypes.GetOrders), Is.False);
            Assert.That(handler.CanHandle(TaskTypes.GetProductInventory), Is.False);
        }

        [Test]
        public void Execute_maps_rows_to_customer_records()
        {
            var rows = new[]
            {
                Row(11000, "AW00011000", "Jon", "Yang", "jon24@adventure-works.com",
                    "1 (11) 500 555-0162", "3761 N. 14th St", "Rockhampton", "Queensland", "4700", "Australia"),
                Row(11001, "AW00011001", "Eugene", "Huang", "eugene10@adventure-works.com",
                    "1 (11) 500 555-0110", "2243 W St.", "Seaford", "Victoria", "3198", "Australia")
            };
            var handler = new GetCustomersHandler(FactoryReturning(rows, out _));

            var result = handler.Execute(NewTask());

            Assert.That(result.Status, Is.EqualTo("completed"));
            Assert.That(result.RecordCount, Is.EqualTo(2));
            Assert.That(result.ErrorMessage, Is.Null);

            var first = (CustomerRecord)result.Data[0];
            Assert.That(first.CustomerId, Is.EqualTo(11000));
            Assert.That(first.AccountNumber, Is.EqualTo("AW00011000"));
            Assert.That(first.FirstName, Is.EqualTo("Jon"));
            Assert.That(first.EmailAddress, Is.EqualTo("jon24@adventure-works.com"));
            Assert.That(first.StateProvince, Is.EqualTo("Queensland"));
            Assert.That(first.CountryRegion, Is.EqualTo("Australia"));

            var second = (CustomerRecord)result.Data[1];
            Assert.That(second.LastName, Is.EqualTo("Huang"));
        }

        [Test]
        public void Execute_tolerates_null_optional_columns()
        {
            var rows = new[]
            {
                Row(12000, "AW00012000", "Cora", "Reed", null, null, null, null, null, null, null)
            };
            var handler = new GetCustomersHandler(FactoryReturning(rows, out _));

            var result = handler.Execute(NewTask());

            Assert.That(result.Status, Is.EqualTo("completed"));
            var record = (CustomerRecord)result.Data[0];
            Assert.That(record.CustomerId, Is.EqualTo(12000));
            Assert.That(record.EmailAddress, Is.Null);
            Assert.That(record.Phone, Is.Null);
            Assert.That(record.AddressLine1, Is.Null);
        }

        [Test]
        public void Execute_binds_modifiedSince_as_parameter()
        {
            var cutoff = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            IDbDataParameter captured;
            var handler = new GetCustomersHandler(FactoryReturning(new Dictionary<string, object>[0], out captured));

            handler.Execute(NewTask(cutoff));

            Assert.That(captured.ParameterName, Is.EqualTo("@modifiedSince"));
            Assert.That(captured.Value, Is.EqualTo(cutoff));
        }

        [Test]
        public void Execute_returns_failed_when_connection_throws()
        {
            var factory = new Mock<IDbConnectionFactory>();
            factory.Setup(f => f.CreateConnection()).Throws(new InvalidOperationException("db down"));
            var handler = new GetCustomersHandler(factory.Object);

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
                TaskType = TaskTypes.GetCustomers,
                Parameters = new TaskParameters
                {
                    ModifiedSince = modifiedSince ?? new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            };
        }

        private static Dictionary<string, object> Row(int customerId, params object[] rest)
        {
            var row = new Dictionary<string, object> { ["CustomerID"] = customerId };
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
            return reader.Object;
        }
    }
}
