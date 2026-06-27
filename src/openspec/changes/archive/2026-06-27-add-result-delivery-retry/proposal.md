## Why

Result delivery was best-effort: `SyncLoop` posted once and only logged on failure, so a transient blip (network hiccup, brief 5xx, timeout) silently lost a completed task's result. This change adds a bounded retry so transient failures are survived without inventing a persistent outbox. It is recorded here retroactively — the code was built and committed directly; this change anchors the decisions in the spec to keep the spec-driven record complete.

## What Changes

- Add `ResultPublisher`: posts a result with up to 3 attempts (1 immediate + 2 retries), backing off 1s then 2s. The backoff is interrupted by the service stop signal, the payload is unchanged between attempts, and it never throws — it logs and returns on a non-retryable error or on exhaustion.
- Classify failures: retry `HttpRequestException`, HTTP `5xx`, and HTTP-timeout `TaskCanceledException`; do not retry `4xx` or serialization errors.
- `SyncPlatformClient.PostResult` now throws a status-carrying `PlatformResponseException` on a non-success status (instead of `EnsureSuccessStatusCode`), so the retry policy can tell `5xx` from `4xx` — `HttpRequestException` does not carry the status on .NET Framework.
- `SyncLoop` delegates result posting to `ResultPublisher`, sharing its `ManualResetEvent` stop signal so `OnStop` interrupts an in-progress backoff.
- Add 4 unit tests: transient-then-success, permanent 5xx exhaustion, non-retryable 4xx, and stop-during-backoff.

## Capabilities

### New Capabilities
- `result-delivery-retry`: posting a task result with a bounded, backed-off retry on transient failures — the attempt budget, the interruptible backoff, the retryable/non-retryable classification, the unchanged payload, and the logging/never-throw behavior.

### Modified Capabilities
- `platform-client`: `PostResult` now signals a non-success HTTP status by throwing a status-carrying exception, so callers can distinguish retryable from non-retryable responses.

## Impact

- New code: `ResultPublisher`, `Http/PlatformResponseException`, and the publisher test. `SyncLoop` and `SyncPlatformClient.PostResult` change; `GetNextTask` and the other HTTP path are untouched.
- No new dependencies (no Polly), no persistent outbox.
- Already implemented and committed; this change is the spec record. Tasks are marked complete.
