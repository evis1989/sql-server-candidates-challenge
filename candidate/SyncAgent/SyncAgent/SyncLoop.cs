using System;
using System.Threading;
using SyncAgent.Configuration;
using SyncAgent.Http;
using SyncAgent.Models;
using SyncAgent.Tasks;

namespace SyncAgent
{
    /// <summary>
    /// Polls the platform, dispatches tasks, posts results. Runs on a dedicated
    /// thread until <see cref="Stop"/> is signalled. Never crashes on a task or
    /// transport error — it logs (and posts a failed result) and keeps polling.
    /// </summary>
    public class SyncLoop
    {
        private readonly ISyncPlatformClient _client;
        private readonly TaskDispatcher _dispatcher;
        private readonly TimeSpan _pollingInterval;
        private readonly ManualResetEvent _stop = new ManualResetEvent(false);
        private readonly ResultPublisher _publisher;

        public SyncLoop(ISyncPlatformClient client, TaskDispatcher dispatcher, AppSettings settings)
        {
            _client = client;
            _dispatcher = dispatcher;
            _pollingInterval = TimeSpan.FromSeconds(settings.PollingIntervalSeconds);
            // Same stop signal so a retry backoff is interrupted by OnStop.
            _publisher = new ResultPublisher(client, _stop);
        }

        /// <summary>Runs the loop on the calling thread until <see cref="Stop"/> is signalled.</summary>
        public void Run()
        {
            Log.Info("Sync loop started.");
            while (!_stop.WaitOne(0))
            {
                try
                {
                    var task = _client.GetNextTask();
                    if (task == null)
                    {
                        // Queue empty: sleep, but wake immediately if asked to stop.
                        if (_stop.WaitOne(_pollingInterval)) break;
                        continue;
                    }

                    ProcessTask(task);
                }
                catch (Exception ex)
                {
                    // Transport/unexpected error with no task in hand: log and keep polling.
                    Log.Error("Polling iteration failed: " + ex.Message);
                    if (_stop.WaitOne(_pollingInterval)) break;
                }
            }
            Log.Info("Sync loop stopped.");
        }

        private void ProcessTask(SyncTask task)
        {
            SyncResult result;
            try
            {
                result = _dispatcher.Dispatch(task);
            }
            catch (Exception ex)
            {
                result = SyncResult.Failed(task.TaskId, task.TaskType, ex.Message);
            }

            // Publisher handles transient retries, backoff, and logging; it never throws.
            _publisher.Publish(result);
        }

        /// <summary>Signals the loop to exit at the next safe point and wakes it if sleeping.</summary>
        public void Stop()
        {
            _stop.Set();
        }
    }
}
