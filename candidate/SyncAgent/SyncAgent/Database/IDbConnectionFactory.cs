using System.Data;

namespace SyncAgent.Database
{
    /// <summary>Creates connections to the local SQL Server database.</summary>
    public interface IDbConnectionFactory
    {
        /// <summary>Returns a new, unopened connection.</summary>
        IDbConnection CreateConnection();
    }
}
