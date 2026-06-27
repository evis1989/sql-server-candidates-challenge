using SyncAgent.Models;

namespace SyncAgent.Tasks
{
    /// <summary>Executes a single type of sync task.</summary>
    public interface ITaskHandler
    {
        /// <summary>True if this handler processes the given task type.</summary>
        bool CanHandle(string taskType);

        /// <summary>Runs the task and returns its result.</summary>
        SyncResult Execute(SyncTask task);
    }
}
