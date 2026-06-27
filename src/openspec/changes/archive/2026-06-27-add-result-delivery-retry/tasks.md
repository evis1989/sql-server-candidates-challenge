> Recorded retroactively — the code was implemented and committed before this change was written, so all tasks are already complete. This change anchors the decisions in the spec.

## 1. Surface the HTTP status

- [x] 1.1 Add `Http/PlatformResponseException.cs` carrying `HttpStatusCode`
- [x] 1.2 `SyncPlatformClient.PostResult` throws `PlatformResponseException` on a non-success status instead of `EnsureSuccessStatusCode` (leave `GetNextTask` untouched)

## 2. ResultPublisher

- [x] 2.1 Add `ResultPublisher` taking `ISyncPlatformClient` and a `WaitHandle` stop signal; public ctor uses the fixed `{1s, 2s}` backoff, a second ctor takes a custom schedule (test seam); `attempts = backoffs.Count + 1`
- [x] 2.2 `Publish` loops attempts: post → return on success; classify on failure; non-retryable → log and return; exhausted → log `taskId`/`taskType`/attempts/"not confirmed" and return; otherwise interruptible `stopSignal.WaitOne(backoff)` then retry. Never throws
- [x] 2.3 `IsRetryable`: `HttpRequestException`, `TaskCanceledException` (timeout — no token passed), and `PlatformResponseException` with `StatusCode >= 500`; everything else (4xx, serialization) is not retryable

## 3. Wire into the loop

- [x] 3.1 `SyncLoop` constructs a `ResultPublisher` with its own `_stop` `ManualResetEvent` and replaces the `PostResult` try/catch in `ProcessTask` with `_publisher.Publish(result)`

## 4. Tests

- [x] 4.1 Transient failure then success → result delivered (`PostResult` called twice)
- [x] 4.2 Permanent 5xx → exactly 3 attempts, logs `taskId` + attempt count, does not throw
- [x] 4.3 HTTP 400 → not retried, single attempt
- [x] 4.4 Stop during backoff → returns at once (elapsed < 500ms), single attempt

## 5. Verification

- [x] 5.1 `dotnet build` clean (0 warnings, 0 errors) and `dotnet test` green (29/29)
