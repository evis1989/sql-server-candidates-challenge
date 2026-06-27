using System.Collections.Generic;
using System.Linq;
using SyncAgent.Models;

namespace SyncAgent.Tasks
{
    /// <summary>Routes a task to the handler that can process its type.</summary>
    public class TaskDispatcher
    {
        private readonly IReadOnlyList<ITaskHandler> _handlers;

        public TaskDispatcher(IEnumerable<ITaskHandler> handlers)
        {
            _handlers = handlers?.ToList() ?? new List<ITaskHandler>();
        }

        /// <summary>
        /// Finds the handler for the task type and executes it. Returns a failed
        /// result (never throws) when no handler is registered for the type.
        /// </summary>
        public SyncResult Dispatch(SyncTask task)
        {
            var handler = _handlers.FirstOrDefault(h => h.CanHandle(task.TaskType));
            if (handler == null)
                return SyncResult.Failed(task.TaskId, task.TaskType,
                    "No handler registered for task type '" + task.TaskType + "'.");

            return handler.Execute(task);
        }
    }
}
