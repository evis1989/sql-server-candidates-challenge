using System;
using System.Data;
using SyncAgent.Database;
using SyncAgent.Models;

namespace SyncAgent.Tasks.Handlers
{
    /// <summary>
    /// Shared base for task handlers backed by a single parameterized SQL query.
    /// Owns the connection lifetime and the completed/failed contract; subclasses
    /// declare only their task type, SQL, and row mapping. Row reading is delegated
    /// to <see cref="DbQuery"/>, the same primitive multi-query handlers use.
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
                using (var connection = _connectionFactory.CreateConnection())
                {
                    connection.Open();
                    var rows = DbQuery.ReadRows(
                        connection,
                        Sql,
                        command => DbQuery.AddParameter(command, "@modifiedSince", task.Parameters.ModifiedSince),
                        MapRow);
                    return SyncResult.Completed(task.TaskId, task.TaskType, rows);
                }
            }
            catch (Exception ex)
            {
                return SyncResult.Failed(task.TaskId, task.TaskType, ex.Message);
            }
        }

        /// <summary>Reads a string column, returning null on DBNull.</summary>
        protected static string GetStringOrNull(IDataReader reader, string column)
        {
            return DbQuery.GetStringOrNull(reader, column);
        }
    }
}
