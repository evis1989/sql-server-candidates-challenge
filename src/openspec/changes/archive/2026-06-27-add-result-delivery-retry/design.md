## Context

`SyncLoop.ProcessTask` posted the result once and logged on failure, so a transient blip lost the result. The fix is a bounded retry. The constraints were fixed up front: 3 attempts (1 + 2), backoff 1s/2s, retry on transport/timeout/5xx, no retry on 4xx/serialization, backoff interruptible by the stop signal, payload stable, log on exhaustion, no Polly, no outbox, no refactor of the other HTTP path. This document records the design decisions taken while implementing it.

## Goals / Non-Goals

**Goals:** survive transient delivery failures within a fixed attempt budget; keep the loop crash-free; keep the new behavior unit-testable without real network or real time.

**Non-Goals:** no persistent outbox / durable queue; no Polly or other retry dependency; no retry on the `GetNextTask` path; no change to the polling cadence.

## Decisions

- **Extract `ResultPublisher` (concrete class), not a private method, and no `IResultPublisher` interface.** Option A allowed either a private `PublishWithRetry` in `SyncLoop` or an extracted publisher. Extracting makes the four behaviors testable in isolation (construct the publisher with a mocked `ISyncPlatformClient` and a real `ManualResetEvent`) without driving the whole polling loop. An interface was **not** added: nothing mocks the publisher itself, so `IResultPublisher` would be indirection with no test or runtime benefit — `ponytail:` add it only if a second implementation or a publisher mock ever appears.
- **`PlatformResponseException` carries the HTTP status.** To retry `5xx` but not `4xx`, the policy needs the status code. On .NET Framework 4.8 `HttpRequestException` does not expose it (added in .NET 5). So `PostResult` replaces `EnsureSuccessStatusCode` with an explicit `IsSuccessStatusCode` check that throws `PlatformResponseException(StatusCode, …)`. This is the smallest change that makes classification possible; `GetNextTask` is untouched.
- **Classification by exception type.** `5xx` → `PlatformResponseException` with `StatusCode >= 500` → retry; `4xx` → same type, `< 500` → no retry; transport → `HttpRequestException` → retry; timeout → `TaskCanceledException` → retry; serialization (`JsonException`) and anything else → no retry. Non-retryable errors fail after the first attempt.
- **`TaskCanceledException` is always a timeout here.** The HTTP call is made without a `CancellationToken`, so a cancellation can only come from the `HttpClient` timeout, never from the service stop. That is why it is unconditionally retryable. The stop case is handled one level up, in the backoff wait — not by cancelling the in-flight request.
- **Interruptible backoff via the shared stop signal.** `SyncLoop` passes its own `ManualResetEvent _stop` to the publisher. The backoff is `stopSignal.WaitOne(delay)`: if the signal is set it returns `true` immediately and the publisher logs and returns, so `OnStop` ends retrying at once instead of waiting out 1s/2s.
- **Injectable backoff schedule as a test seam.** The public constructor uses the fixed `{1s, 2s}` schedule (so production behavior matches the rule). A second constructor takes a custom schedule; the transient-success and exhaustion tests pass `TimeSpan.Zero` to run fast, while the stop-during-backoff test keeps the real 1s to prove the stop actually interrupts it (asserting elapsed < 500ms). `attempts = backoffs.Count + 1`, so the default schedule yields exactly 3 attempts.
- **Never throws.** `Publish` catches every exception, classifies, logs, and returns — the polling loop's "never crash" contract is preserved and `ProcessTask` no longer needs its own try/catch around posting.

## Risks / Trade-offs

- **A genuinely-down platform costs up to ~3s per task** (1s + 2s backoff) before the loop moves on. Acceptable for a polling agent; the stop signal cuts it short on shutdown. If it ever mattered, the schedule is one array to change.
- **Console-captured logging assertion in the exhaustion test** couples that test to the logger writing to `Console`. Cheap and dependency-free; if logging is ever made injectable the test simplifies.
- **No durability:** if the process dies mid-backoff the result is lost (no outbox, by decision). The platform re-queues unconfirmed tasks in this architecture, so a persistent outbox was deliberately out of scope.
