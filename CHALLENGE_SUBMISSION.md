# Challenge Submission

## Candidate

- **Name:** Evis Delgado
- **Date:** 2026-06-27

---

## How to Run

**Prerequisites**

- .NET Framework 4.8 Developer Pack (the agent targets `net48` deliberately — see Architecture Decisions).
- The .NET SDK / `dotnet` CLI (used to build and test; it builds the `net48` projects).
- SQL Server with **AdventureWorks2025** restored (see `README.md` for restore steps).
- The **SyncPlatform** test app running on `http://localhost:5100`.

**Build and test**

```bash
cd candidate/SyncAgent
dotnet build -c Release
dotnet test
```

**Configure**

Edit `candidate/SyncAgent/SyncAgent/App.config` and set `DatabaseConnectionString` to your SQL Server instance. The other three keys (`PlatformBaseUrl`, `PlatformApiKey`, `PollingIntervalSeconds`) are pre-filled to match the challenge defaults.

**Run (console / debug mode)**

```bash
cd candidate/SyncAgent
dotnet run --project SyncAgent
```

The entry point detects `Environment.UserInteractive` and runs the polling loop in the console; press **Enter** to stop. With the platform unavailable it logs the error and keeps polling — it never crashes.

**Run as a Windows Service**

Build, then register the produced `SyncAgent.exe` with the Service Control Manager:

```cmd
sc create KabilioSyncAgent binPath= "C:\path\to\SyncAgent\bin\Release\net48\SyncAgent.exe"
sc start KabilioSyncAgent
```

There is no MSI installer (out of scope per the challenge); service installation is manual via `sc.exe`.

**Verify the SQL against a live database**

The unit tests mock the data reader, so they do not exercise the real schema. Run `candidate/SyncAgent/sql/smoke-test.sql` against AdventureWorks2025 to confirm each of the four queries resolves and returns the documented shape:

```bash
sqlcmd -S localhost -d AdventureWorks2025 -E -i candidate/SyncAgent/sql/smoke-test.sql
```

---

## Architecture Decisions

**Windows Service on .NET Framework 4.8.** Chosen deliberately to match the role's production stack rather than a greenfield .NET 8 host. The goal was to demonstrate the Windows Service lifecycle (`OnStart`/`OnStop`), not just a console worker.

**Polling loop on a dedicated thread with a stop signal.** `OnStart` launches `SyncLoop.Run` on a background thread and returns immediately so the SCM doesn't time out; `OnStop` sets a `ManualResetEvent` and joins. The loop sleeps on `204` via `WaitOne(interval)`, so a stop request wakes it promptly instead of waiting out the interval. This is the classic, correct pattern for a long-lived service worker — no `async`/`Task` host, no `Thread.Abort`.

**Dependency injection through interfaces for all I/O.** `ISyncPlatformClient` (HTTP) and `IDbConnectionFactory` (DB) are the seams. The loop and handlers depend on the interfaces; concretes are composed in `Program.cs`. This is what makes everything unit-testable against mocks.

**Dispatcher + per-type handlers, failing soft.** `TaskDispatcher` selects a handler by task type; an unknown type returns a `failed` result instead of throwing, so the loop's "never crash" contract holds even before/without a handler.

**A `SqlTaskHandler` base extracted at the second handler, not the first.** The first handler (GetCustomers) was written self-contained — with one implementation there was nothing to share. When GetProducts arrived and the plumbing actually repeated, the shared template (`TaskType` / `Sql` / `MapRow`) was extracted, then the read primitive itself (`DbQuery.ReadRows`) when GetOrders needed two queries on one connection. Abstractions were introduced when duplication proved real, not speculatively.

**GetOrders uses two queries, not a header×detail JOIN.** Headers filtered by `modifiedSince`, then details fetched for those ids via a parameterized `IN (@id0..@idN)` list and stitched in memory. This keeps each query simple and independently testable and avoids duplicating header data across detail rows. Zero headers short-circuits the detail query (an `IN ()` is invalid SQL). The detail ids are queried in **batches of 1000** to stay under SQL Server's 2100-parameter limit, so an arbitrarily large `modifiedSince` window (e.g. an initial full sync) still succeeds.

**Bounded retry on result delivery.** Posting a result is wrapped in a `ResultPublisher` that retries transient failures up to 3 attempts (1 + 2), backing off 1s then 2s. It retries transport errors, HTTP timeouts, and HTTP `5xx`, but **not** `4xx` or serialization errors (retrying those can't help). The backoff is interrupted by the service stop signal, the payload is identical across attempts, and it never throws — on exhaustion or a non-retryable error it logs and returns. Because `HttpRequestException` does not carry the status code on .NET Framework, `PostResult` throws a small `PlatformResponseException` carrying it so the policy can tell `5xx` from `4xx`. No Polly and no persistent outbox were added — the platform re-queues unconfirmed tasks, so a durable queue was out of scope.

**Spec-driven workflow (OpenSpec).** Each change — the scaffold, the four handlers, the result-delivery retry, and the GetOrders id-batching fix — was a separate change with a proposal, spec, design, and tasks before any code, captured under `src/openspec/`.

---

## Security Measures

- **Parameterized queries exclusively.** Every value — `@modifiedSince` and the GetOrders `IN` list — is bound as a command parameter. The `IN` clause generates only parameter *names* (`@id0`, `@id1`, …) into the SQL text; the id *values* are never concatenated, so there is no SQL injection surface.
- **API key from configuration, not hardcoded.** Loaded from `App.config` into `AppSettings` and sent as the `X-Api-Key` header on every platform request.
- **No sensitive data logged.** The logger records lifecycle and error messages only, not row data or credentials.
- **Fail-soft, no information leakage on crash.** Errors are caught, logged, and reported back as `failed` results; the service does not crash or surface stack traces to the platform.
- **Retry does not hammer client/auth errors.** The result-delivery retry treats `4xx` (including auth/contract failures) as non-retryable and stops after the first attempt — only genuinely transient failures (`5xx`, timeouts, transport) are retried.

---

## Testing Strategy

**30 unit tests, all green**, run with `dotnet test`.

- **What is tested:** each handler's `CanHandle`, row mapping (including decimals, dates, and tinyint/smallint integer reads), and `modifiedSince` parameter binding; the GetOrders two-query stitch, the empty-headers short-circuit, its `IN` parameter binding, and the detail-query batching (a large id set split into multiple batches); the dispatcher's fail-soft on unknown types; `SyncResult` serialization (`errorMessage` always present, `null` when empty); the platform client's `200`→task / `204`→null handling; and the result-delivery retry (transient-then-success, permanent `5xx` exhaustion, non-retryable `4xx`, and stop-during-backoff).
- **How:** the ADO.NET interfaces (`IDbConnection` / `IDbCommand` / `IDataReader`) and `HttpMessageHandler` are mocked, so handlers and the client are tested without a live database or server.
- **What is deliberately not unit-tested:** the SQL against the real schema — a mocked reader can't prove the joins/columns exist. That gap is covered by `sql/smoke-test.sql`, to be run once against a live AdventureWorks2025.
- **With more time:** integration tests against a restored AdventureWorks2025 and an end-to-end run against the SyncPlatform app.

---

## Known Limitations

- **SQL not verified against a live database in CI.** Tests mock the reader; `sql/smoke-test.sql` is provided for manual verification before submission.
- **GetCustomers takes the first email/phone/address per customer** (by id order) to produce one flat row, as the contract is flat. A customer with multiple contacts returns the first.
- **`customerName` is null for store orders** (customers with no associated person), consistent with the flat contract.
- **No MSI/WiX installer.** Service installation is manual via `sc.exe` (installer explicitly out of scope).
- **Result delivery has retry but no durable outbox.** Posting a result retries transient failures (3 attempts, 1s/2s backoff), but if the process dies mid-retry the result is lost — there is no persistent queue. By design: the platform re-queues unconfirmed tasks, so a durable outbox was out of scope.

---

## AI Tools Used

This solution was built with **Claude Code (Claude Opus 4.8)** driving an **OpenSpec spec-driven workflow**. Specifically:

- Each piece — the scaffold, each of the four handlers, the result-delivery retry, and the GetOrders id-batching fix — was developed as a separate OpenSpec change: `propose` (proposal + spec + design + tasks), then `apply` (implementation + tests), then `archive`.
- I (the candidate) **reviewed and explicitly approved every design decision** before implementation — e.g. the dedicated-thread polling pattern, when to extract the `SqlTaskHandler` base and the `DbQuery` primitive, the two-query approach for GetOrders, the `Convert.ToInt32` reads for tinyint/smallint, serializing `errorMessage` as explicit `null`, the bounded-retry policy and its retryable/non-retryable classification, and batching the GetOrders detail lookup under the parameter cap.
- Claude wrote the C# code, the unit tests, the SQL queries, and the OpenSpec artifacts; I directed the architecture, set constraints, and verified build/test results at each step.

---

## Time Spent

- Environment setup (Docker, SQL Server, AdventureWorks restore, OpenSpec): ~ 1 hr
- Scaffold (solution, service, polling loop, seams, models, config): ~ 20 min
- GetCustomers handler + establishing the pattern: ~ 15 min
- GetProducts handler + `SqlTaskHandler` base extraction: ~ 15 min
- GetOrders handler (two-query + stitch) + `DbQuery` extraction: ~ 20 min
- GetProductInventory handler: ~ 10 min
- Smoke script + submission write-up: ~ 10 min
- Result-delivery retry (publisher, classification, tests): _[~ min — fill in]_
- GetOrders id-batching fix + live-DB debugging (Docker SQL auth, data date range): _[~ min — fill in]_
- **Total: ~ 3 hrs** _(plus the retry/batching/debugging above — update)_

---

## Feedback

The challenge is well-designed and honest about what it's testing. The API contract and sample payloads in docs/ are clear enough to build against without ambiguity. The most time-consuming part was environment setup — getting AdventureWorks2025 restored on Docker with the right SQL Server version took longer than expected. The actual development was fast once the environment was ready, which I think reflects the real job: most of the friction in this kind of system is operational, not algorithmic. One suggestion: a docker-compose with SQL Server + AdventureWorks pre-seeded would let candidates focus on the code from minute one.
