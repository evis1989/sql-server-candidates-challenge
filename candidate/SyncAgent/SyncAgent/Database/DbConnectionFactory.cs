using System.Data;
using System.Data.SqlClient;
using SyncAgent.Configuration;

namespace SyncAgent.Database
{
    /// <summary>Creates SqlConnection instances from the configured connection string.</summary>
    public class DbConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public DbConnectionFactory(AppSettings settings)
        {
            _connectionString = settings.DatabaseConnectionString;
        }

        public IDbConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}
