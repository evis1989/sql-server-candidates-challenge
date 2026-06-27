## Context

The agent targets .NET Framework 4.8 as a Windows Service, deliberately matching Kabilio's production stack rather than a greenfield .NET 8 host. This change builds only the skeleton: it must compile and the service must start/stop cleanly, with the task-handling and SQL layers stubbed behind seams so later changes plug in without reworking the plumbing. Requirements come from `specs/sync-agent-runtime` and `specs/platform-client`; the wire contract comes from `docs/api-contract.md`.

## Goals / Non-Goals

**Goals:**

- A buildable `SyncAgent.sln` with a service project and an NUnit test project.
- Service lifecycle (`OnStart`/`OnStop`) that drives a stoppable polling loop on a background thread.
- I/O seams (`ISyncPlatformClient`, `IDbConnectionFactory`) so the loop and future handlers are unit-testable against mocks.
- Models and JSON serialization that match the contract, with `errorMessage` always emitted (null when empty).
- An empty `TaskDispatcher` registry that degrades to a `failed` result for unknown types.

**Non-Goals:**

- No task handlers and no SQL queries (next change).
- No MSI/WiX installer (out of scope per project.md).
- No retry/backoff strategy beyond "sleep on 204, continue on error".

## Decisions

- **Two projects, folders mirror project.md.** `SyncAgent` (service) + `SyncAgent.Tests` (NUnit). Folder layout follows the structure already specified in `openspec/project.md` so the agreed map is the source of truth. Alternative — a single project with embedded tests — rejected; the role wants a clean test project boundary.
- **Polling loop on a background thread with cancellation.** `SyncAgentService.OnStart` spins up the loop and returns immediately so the SCM doesn't time out; `OnStop` signals stop and joins. Use a `volatile bool`/`ManualResetEvent` stop signal rather than `Thread.Abort`. .NET Framework 4.8 has `CancellationToken`, but a simple stop flag + `WaitHandle` for the interruptible sleep keeps it boring and lets `OnStop` wake a sleeping loop promptly. `ponytail:` no `Task`/`async` loop host — a dedicated thread is the simplest correct fit for a long-lived service worker.
- **Seam interfaces for all I/O.** `ISyncPlatformClient` (GetNextTask → `SyncTask`/null, PostResult) and `IDbConnectionFactory` (CreateConnection → `IDbConnection`). The loop depends on the interfaces; concretes are constructed in `Program`/composition. Tests mock the interface, never the concrete — matches project.md conventions.
- **Newtonsoft.Json with explicit null handling.** Serialize `SyncResult` with `NullValueHandling.Include` (the default) so `errorMessage` is always present as `null` when empty. Decision per the user: explicit null over omission — safer and matches the contract's "null or string". Property names are camelCase to match the sample payloads.
- **Empty dispatcher that fails soft.** `TaskDispatcher` holds a list of `ITaskHandler` (empty for now). `Dispatch` finds the first handler whose `CanHandle(taskType)` is true; if none, it returns a `failed` `SyncResult` with a "no handler for <type>" message. This keeps the loop's contract ("never crash, post failed on error") intact before any handler exists, and is directly testable.
- **`HttpClient` reused, not per-request.** A single configured `HttpClient` (base address + `X-Api-Key` default header) lives for the client's lifetime to avoid socket exhaustion. Standard guidance; trivial here but correct.

## Risks / Trade-offs

- **Background-thread loop swallows errors to stay alive** → every caught error is logged and (where a task is in flight) posted as a `failed` result, so failures are observable rather than silent. A self-check test asserts the dispatcher returns `failed` (not throws) for an unknown type.
- **No real DB/handlers yet means little to run end-to-end** → mitigated by unit tests on the loop (mocked client/dispatcher), the dispatcher's fail-soft path, and serialization of `SyncResult`. "Compiles + starts clean" is verified by building the solution and the service entry point.
- **.NET Framework 4.8 build on this machine** → if the targeting pack / MSBuild isn't present, the build step will surface it early; flagged as the first thing to confirm in tasks.
