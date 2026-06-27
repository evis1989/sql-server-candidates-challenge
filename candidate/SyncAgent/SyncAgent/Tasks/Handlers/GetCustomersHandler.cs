using System;
using System.Collections.Generic;
using System.Data;
using SyncAgent.Database;
using SyncAgent.Models;

namespace SyncAgent.Tasks.Handlers
{
    /// <summary>
    /// Executes GetCustomers tasks: queries AdventureWorks2025 for customers modified
    /// since the task's cut-off and maps each to the flat customer contract shape.
    /// Reference implementation for the other task handlers.
    /// </summary>
    public class GetCustomersHandler : ITaskHandler
    {
        // Filter on the Customer entity's own ModifiedDate (the sync trigger).
        // ponytail: one flat row per customer — first email/phone/address only,
        // because the contract wants a single address, not an array.
        private const string Sql = @"
SELECT
    c.CustomerID,
    c.AccountNumber,
    p.FirstName,
    p.LastName,
    em.EmailAddress,
    ph.PhoneNumber   AS Phone,
    a.AddressLine1,
    a.City,
    sp.Name          AS StateProvince,
    a.PostalCode,
    cr.Name          AS CountryRegion
FROM Sales.Customer c
INNER JOIN Person.Person p
    ON p.BusinessEntityID = c.PersonID
OUTER APPLY (
    SELECT TOP 1 e.EmailAddress
    FROM Person.EmailAddress e
    WHERE e.BusinessEntityID = p.BusinessEntityID
    ORDER BY e.EmailAddressID
) em
OUTER APPLY (
    SELECT TOP 1 pp.PhoneNumber
    FROM Person.PersonPhone pp
    WHERE pp.BusinessEntityID = p.BusinessEntityID
    ORDER BY pp.PhoneNumberTypeID
) ph
OUTER APPLY (
    SELECT TOP 1 bea.AddressID
    FROM Person.BusinessEntityAddress bea
    WHERE bea.BusinessEntityID = p.BusinessEntityID
    ORDER BY bea.AddressID
) addr
LEFT JOIN Person.Address a
    ON a.AddressID = addr.AddressID
LEFT JOIN Person.StateProvince sp
    ON sp.StateProvinceID = a.StateProvinceID
LEFT JOIN Person.CountryRegion cr
    ON cr.CountryRegionCode = sp.CountryRegionCode
WHERE c.ModifiedDate >= @modifiedSince
ORDER BY c.CustomerID;";

        private readonly IDbConnectionFactory _connectionFactory;

        public GetCustomersHandler(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public bool CanHandle(string taskType)
        {
            return taskType == TaskTypes.GetCustomers;
        }

        public SyncResult Execute(SyncTask task)
        {
            try
            {
                var rows = Query(task.Parameters.ModifiedSince);
                return SyncResult.Completed(task.TaskId, task.TaskType, rows);
            }
            catch (Exception ex)
            {
                return SyncResult.Failed(task.TaskId, task.TaskType, ex.Message);
            }
        }

        private IReadOnlyList<object> Query(DateTime modifiedSince)
        {
            var rows = new List<object>();
            using (var connection = _connectionFactory.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = Sql;

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@modifiedSince";
                parameter.Value = modifiedSince;
                command.Parameters.Add(parameter);

                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        rows.Add(Map(reader));
                }
            }
            return rows;
        }

        private static CustomerRecord Map(IDataReader reader)
        {
            return new CustomerRecord
            {
                CustomerId = reader.GetInt32(reader.GetOrdinal("CustomerID")),
                AccountNumber = GetStringOrNull(reader, "AccountNumber"),
                FirstName = GetStringOrNull(reader, "FirstName"),
                LastName = GetStringOrNull(reader, "LastName"),
                EmailAddress = GetStringOrNull(reader, "EmailAddress"),
                Phone = GetStringOrNull(reader, "Phone"),
                AddressLine1 = GetStringOrNull(reader, "AddressLine1"),
                City = GetStringOrNull(reader, "City"),
                StateProvince = GetStringOrNull(reader, "StateProvince"),
                PostalCode = GetStringOrNull(reader, "PostalCode"),
                CountryRegion = GetStringOrNull(reader, "CountryRegion")
            };
        }

        private static string GetStringOrNull(IDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }
    }
}
