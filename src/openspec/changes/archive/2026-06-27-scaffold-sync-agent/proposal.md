## Why

Kabilio's production architecture runs sync agents as always-on Windows Services that poll a central platform, query a local SQL Server, and post results back. Before any task-specific logic (handlers, SQL) can be built and tested, the project needs a compiling skeleton: the service host, the polling loop, the I/O interfaces, and the platform communication contract. This change establishes that foundation so subsequent changes can drop in handlers without touching plumbing.

## What Changes

- Create the `candidate/SyncAgent` solution: a .NET Framework 4.8 Windows Service project plus an NUnit test project, wired into `SyncAgent.sln`.
- Add the service host: `Program.cs` (entry point) and `SyncAgentService.cs` (`OnStart`/`OnStop` lifecycle).
- Add `SyncLoop`: the polling loop that fetches a task, dispatches it, and posts the result, sleeping on `204` and never crashing on error.
- Add the platform HTTP client: `ISyncPlatformClient` + `SyncPlatformClient` (GET `next-task`, POST `result`, `X-Api-Key` auth).
- Add the DB seam: `IDbConnectionFactory` + `DbConnectionFactory` (creates `SqlConnection` from config). No queries yet.
- Add models: `SyncTask`, `TaskParameters`, `SyncResult`. `SyncResult.errorMessage` always serializes (as `null` when empty).
- Add `TaskDispatcher` that resolves a handler by task type — registry is empty for now; an unknown/unhandled type yields a `failed` result rather than a throw.
- Add `App.config` with the four keys (`PlatformBaseUrl`, `PlatformApiKey`, `DatabaseConnectionString`, `PollingIntervalSeconds`) and strongly-typed `AppSettings`.
- No task handlers and no SQL in this change. The solution must compile and the service must start and stop cleanly.

## Capabilities

### New Capabilities
- `sync-agent-runtime`: the Windows Service lifecycle and polling loop — start/stop behavior, fetch→dispatch→post orchestration, sleep-on-204, and crash-resistant error handling.
- `platform-client`: the HTTP contract with the sync platform — authenticated GET `next-task` (200/204) and POST `result`, including the result payload serialization rules.

### Modified Capabilities

(none — greenfield scaffold)

## Impact

- New code under `candidate/SyncAgent/` (no existing code is modified).
- New dependencies: Newtonsoft.Json (serialization), NUnit + Moq (tests). `System.Net.Http` and `System.ServiceProcess` from the framework.
- Requires .NET Framework 4.8 to build. Runtime DB connection targets AdventureWorks2025 but no queries execute yet.
