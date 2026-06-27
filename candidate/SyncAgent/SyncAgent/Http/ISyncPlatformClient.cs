using SyncAgent.Models;

namespace SyncAgent.Http
{
    /// <summary>Communicates with the sync platform API.</summary>
    public interface ISyncPlatformClient
    {
        /// <summary>GET next-task. Returns the task, or null on 204 (queue empty).</summary>
        SyncTask GetNextTask();

        /// <summary>POST the result of an executed task.</summary>
        void PostResult(SyncResult result);
    }
}
