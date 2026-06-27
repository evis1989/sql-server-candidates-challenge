using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SyncAgent.Http;
using SyncAgent.Models;

namespace SyncAgent
{
    /// <summary>
    /// Posts a task result with a bounded retry on transient failures. Three attempts
    /// total (1 immediate + 2 retries), backing off 1s then 2s. The backoff is
    /// interruptible by the service stop signal, and the payload never changes between
    /// attempts. Never throws — on non-retryable errors or exhaustion it logs and returns.
    /// </summary>
    public class ResultPublisher
    {
        // Delays before attempts 2 and 3 → 3 attempts total.
        private static readonly TimeSpan[] DefaultBackoffs =
        {
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2)
        };

        private readonly ISyncPlatformClient _client;
        private readonly WaitHandle _stopSignal;
        private readonly IReadOnlyList<TimeSpan> _backoffs;

        public ResultPublisher(ISyncPlatformClient client, WaitHandle stopSignal)
            : this(client, stopSignal, DefaultBackoffs)
        {
        }

        // Test seam: custom backoff schedule. attempts = backoffs.Count + 1.
        public ResultPublisher(ISyncPlatformClient client, WaitHandle stopSignal, IReadOnlyList<TimeSpan> backoffs)
        {
            _client = client;
            _stopSignal = stopSignal;
            _backoffs = backoffs;
        }

        /// <summary>Posts the result, retrying transient failures with backoff.</summary>
        public void Publish(SyncResult result)
        {
            var maxAttempts = _backoffs.Count + 1;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    _client.PostResult(result);
                    return; // delivered
                }
                catch (Exception ex)
                {
                    if (!IsRetryable(ex))
                    {
                        Log.Error("Result for task " + result.TaskId + " (" + result.TaskType +
                                  ") failed and will not be retried: " + ex.Message);
                        return;
                    }

                    if (attempt == maxAttempts)
                    {
                        Log.Error("Result for task " + result.TaskId + " (" + result.TaskType +
                                  ") not confirmed after " + maxAttempts + " attempts; delivery failed.");
                        return;
                    }

                    // Interruptible backoff: a stop signal ends retrying immediately.
                    if (_stopSignal.WaitOne(_backoffs[attempt - 1]))
                    {
                        Log.Info("Result delivery for task " + result.TaskId +
                                 " aborted during backoff (service stopping).");
                        return;
                    }
                }
            }
        }

        /// <summary>Transient failures worth retrying: transport error, timeout, or HTTP 5xx.</summary>
        private static bool IsRetryable(Exception ex)
        {
            if (ex is PlatformResponseException response)
                return (int)response.StatusCode >= 500;

            // No CancellationToken is passed to the HTTP call, so a TaskCanceledException
            // is always an HttpClient timeout (never the service stop) — retry it.
            return ex is HttpRequestException || ex is TaskCanceledException;
        }
    }
}
