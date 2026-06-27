## ADDED Requirements

### Requirement: Authenticated requests

The platform client SHALL send the `X-Api-Key` header, sourced from configuration, on every request to the platform.

#### Scenario: API key attached

- **WHEN** the client issues any request to the platform
- **THEN** the request includes the `X-Api-Key` header with the configured key value

### Requirement: Fetch next task

The client SHALL expose a `GetNextTask` operation that returns the parsed task on `200` and `null` on `204`.

#### Scenario: Task returned

- **WHEN** `GET /api/sync/next-task` responds `200` with a task body
- **THEN** the client returns a `SyncTask` deserialized from the body, including `taskId`, `taskType`, `parameters.modifiedSince`, and `createdAt`

#### Scenario: No task queued

- **WHEN** `GET /api/sync/next-task` responds `204`
- **THEN** the client returns `null`

### Requirement: Post task result

The client SHALL expose a `PostResult` operation that serializes a `SyncResult` and submits it to `POST /api/sync/result`.

#### Scenario: Result submitted

- **WHEN** the client posts a `SyncResult`
- **THEN** the request body contains `taskId`, `taskType`, `status`, `data`, `recordCount`, `executedAt`, and `errorMessage`

### Requirement: errorMessage serialization

A `SyncResult` SHALL always serialize the `errorMessage` field; when there is no error its value SHALL be JSON `null` rather than omitted.

#### Scenario: Completed result carries explicit null

- **WHEN** a `completed` result with no error is serialized
- **THEN** the JSON includes `"errorMessage": null`

#### Scenario: Failed result carries the message

- **WHEN** a `failed` result is serialized
- **THEN** the JSON includes `errorMessage` set to the error description string
