## 1. Solution and projects

- [x] 1.1 Confirm .NET Framework 4.8 targeting pack + MSBuild are available on this machine (build a trivial project or check installed SDKs)
- [x] 1.2 Create `candidate/SyncAgent/SyncAgent.sln` with a Windows Service project `SyncAgent` (net48) and a test project `SyncAgent.Tests` (net48)
- [x] 1.3 Add NuGet refs: Newtonsoft.Json to `SyncAgent`; NUnit, NUnit3TestAdapter, Moq to `SyncAgent.Tests`; add framework refs `System.ServiceProcess`, `System.Net.Http`, `System.Configuration`
- [x] 1.4 Create the folder layout under `SyncAgent/` exactly as in `openspec/project.md` (Configuration, Http, Tasks/Handlers, Database, Models)

## 2. Configuration

- [x] 2.1 Add `App.config` with `PlatformBaseUrl`, `PlatformApiKey`, `DatabaseConnectionString`, `PollingIntervalSeconds`
- [x] 2.2 Implement `Configuration/AppSettings.cs` — strongly-typed read of the four keys from `ConfigurationManager`, no hardcoded secrets

## 3. Models

- [x] 3.1 `Models/TaskParameters.cs` (`modifiedSince`) and `Models/SyncTask.cs` (`taskId`, `taskType`, `parameters`, `createdAt`) with camelCase JSON names
- [x] 3.2 `Models/SyncResult.cs` (`taskId`, `taskType`, `status`, `data`, `recordCount`, `executedAt`, `errorMessage`) — `errorMessage` always serialized, null when empty; add `Completed`/`Failed` factory helpers
- [x] 3.3 Add a task-type constants/enum (no magic strings) for `GetCustomers`/`GetProducts`/`GetOrders`/`GetProductInventory`

## 4. I/O seams

- [x] 4.1 `Http/ISyncPlatformClient.cs` — `GetNextTask()` returns `SyncTask` or null, `PostResult(SyncResult)`
- [x] 4.2 `Http/SyncPlatformClient.cs` — single configured `HttpClient` (base URL + `X-Api-Key`), GET next-task (200→task, 204→null), POST result; explicit-null serialization settings
- [x] 4.3 `Database/IDbConnectionFactory.cs` + `Database/DbConnectionFactory.cs` — create a `SqlConnection` from the configured connection string (no queries)

## 5. Dispatch

- [x] 5.1 `Tasks/ITaskHandler.cs` — `bool CanHandle(string taskType)`, `SyncResult Execute(SyncTask task)`
- [x] 5.2 `Tasks/TaskDispatcher.cs` — holds an (empty) list of `ITaskHandler`; `Dispatch` picks the matching handler, returns a `failed` `SyncResult` when none matches (no throw)

## 6. Service host and loop

- [x] 6.1 `SyncLoop.cs` — stoppable loop: fetch → dispatch → post, sleep `PollingIntervalSeconds` on 204, catch/log/post-failed and continue on error; interruptible stop signal
- [x] 6.2 `SyncAgentService.cs` — `OnStart` launches the loop on a background thread and returns; `OnStop` signals stop and joins within the SCM timeout
- [x] 6.3 `Program.cs` — entry point: compose `AppSettings`, client, dispatcher, loop, service; run under `ServiceBase.Run` (and a console/debug path if convenient)

## 7. Tests and verification

- [x] 7.1 `SyncPlatformClientTests` — 204 returns null; 200 deserializes a task (use a stub `HttpMessageHandler`)
- [x] 7.2 `TaskDispatcherTests` — unknown task type returns a `failed` result, not a throw
- [x] 7.3 `SyncResult` serialization test — completed result emits `"errorMessage": null`; failed result emits the message
- [x] 7.4 Build the full solution (Release) and run the test suite green
- [x] 7.5 Smoke-start the service entry point (console/debug mode) and confirm a clean start/stop with the platform unavailable (loop logs and keeps polling, no crash)
