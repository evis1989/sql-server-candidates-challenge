using System;
using System.Collections.Generic;
using System.Data;

namespace SyncAgent.Database
{
    /// <summary>
    /// Shared read primitive for task handlers: runs a parameterized command on an
    /// already-open connection and maps its rows. The caller owns the connection's
    /// lifetime, which lets a handler run several queries on one open connection.
    /// </summary>
    public static class DbQuery
    {
        /// <summary>
        /// Executes <paramref name="sql"/> on the open connection, applies
        /// <paramref name="bindParameters"/>, and maps each row with <paramref name="mapRow"/>.
        /// Does not open or dispose the connection.
        /// </summary>
        public static IReadOnlyList<object> ReadRows(
            IDbConnection openConnection,
            string sql,
            Action<IDbCommand> bindParameters,
            Func<IDataReader, object> mapRow)
        {
            var rows = new List<object>();
            using (var command = openConnection.CreateCommand())
            {
                command.CommandText = sql;
                bindParameters(command);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        rows.Add(mapRow(reader));
                }
            }
            return rows;
        }

        /// <summary>Creates a named parameter with the given value and adds it to the command.</summary>
        public static void AddParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        /// <summary>Reads a string column, returning null on DBNull.</summary>
        public static string GetStringOrNull(IDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }
    }
}
