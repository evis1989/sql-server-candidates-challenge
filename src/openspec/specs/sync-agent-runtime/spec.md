# sync-agent-runtime Specification

## Purpose
TBD - created by archiving change scaffold-sync-agent. Update Purpose after archive.
## Requirements
### Requirement: Windows Service lifecycle

The agent SHALL run as a Windows Service that starts its polling loop on `OnStart` and stops it cleanly on `OnStop` without blocking the Service Control Manager.

#### Scenario: Service starts cleanly

- **WHEN** the Service Control Manager calls `OnStart`
- **THEN** the service starts the polling loop on a background thread and returns control to the SCM without throwing

#### Scenario: Service stops cleanly

- **WHEN** the Service Control Manager calls `OnStop`
- **THEN** the service signals the polling loop to stop, waits for the current iteration to finish, and returns within the SCM timeout

### Requirement: Polling loop orchestration

The polling loop SHALL repeatedly fetch the next task, dispatch it to a handler, and post the result, until a stop is requested.

#### Scenario: Task available

- **WHEN** the platform returns a task from `next-task`
- **THEN** the loop dispatches the task to the matching handler and posts the resulting `SyncResult`, then polls again immediately

#### Scenario: No task queued

- **WHEN** the platform responds with `204 No Content`
- **THEN** the loop sleeps for `PollingIntervalSeconds` and then polls again

#### Scenario: Stop requested mid-loop

- **WHEN** a stop has been signalled
- **THEN** the loop exits at the next safe point and does not start another fetch

### Requirement: Crash-resistant error handling

The polling loop SHALL NOT crash the service on a task or transport error; it SHALL record the failure and continue polling.

#### Scenario: Task execution fails

- **WHEN** dispatching or executing a task throws
- **THEN** the loop posts a `failed` result carrying the error message and continues to the next poll

#### Scenario: Unexpected error in the loop

- **WHEN** an unexpected exception occurs during a loop iteration
- **THEN** the loop logs the error and continues to the next iteration rather than terminating the service

### Requirement: Task dispatch routing

The dispatcher SHALL select a handler by task type from a registry, returning a `failed` result when no handler is registered.

#### Scenario: No handler registered for task type

- **WHEN** a task arrives whose type has no registered handler
- **THEN** the dispatcher returns a `failed` `SyncResult` with an explanatory error message instead of throwing

### Requirement: Configuration from App.config

The agent SHALL load `PlatformBaseUrl`, `PlatformApiKey`, `DatabaseConnectionString`, and `PollingIntervalSeconds` from configuration into a strongly-typed settings object; secrets SHALL NOT be hardcoded.

#### Scenario: Settings loaded at startup

- **WHEN** the service starts
- **THEN** the four configuration values are read from `App.config` into `AppSettings` and used by the loop and clients

