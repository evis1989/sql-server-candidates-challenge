## MODIFIED Requirements

### Requirement: Post task result

The client SHALL expose a `PostResult` operation that serializes a `SyncResult` and submits it to `POST /api/sync/result`. On a non-success HTTP status it SHALL throw an exception that carries the HTTP status code, so callers can distinguish retryable (`5xx`) from non-retryable (`4xx`) responses.

#### Scenario: Result submitted

- **WHEN** the client posts a `SyncResult`
- **THEN** the request body contains `taskId`, `taskType`, `status`, `data`, `recordCount`, `executedAt`, and `errorMessage`

#### Scenario: Non-success status surfaces the status code

- **WHEN** `POST /api/sync/result` responds with a non-success status
- **THEN** the client throws an exception carrying that HTTP status code rather than a generic transport error
