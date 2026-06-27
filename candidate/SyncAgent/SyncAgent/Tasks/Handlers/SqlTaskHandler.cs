using System;
using System.Collections.Generic;
using System.Data;
using SyncAgent.Database;
using SyncAgent.Models;

namespace SyncAgent.Tasks.Handlers
{
    /// <summary>
    /// Shared base for task handlers backed by a single parameterized SQL query.
    /// Owns the connection/command/reader plumbing and the completed/failed contract;
    /// subclasses declare only their task type, SQL, and row mapping.
    /// </summary>
    public abstract class SqlTaskHandler : ITaskHandler
    {
        private readonly IDbConnectionFactory _connectionFactory;

        protected SqlTaskHandler(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        /// <summary>The task type this handler serves.</summary>
        protected abstract string TaskType { get; }

        /// <summary>The parameterized query; SHALL bind <c>@modifiedSince</c>.</summary>
        protected abstract string Sql { get; }

        /// <summary>Maps the current reader row to a result record.</summary>
        protected abstract object MapRow(IDataReader reader);

        public bool CanHandle(string taskType)
        {
            return taskType == TaskType;
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
                        rows.Add(MapRow(reader));
                }
            }
            return rows;
        }

        /// <summary>Reads a string column, returning null on DBNull.</summary>
        protected static string GetStringOrNull(IDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }
    }
}
