# result-delivery-retry Specification

## Purpose
TBD - created by archiving change add-result-delivery-retry. Update Purpose after archive.
## Requirements
### Requirement: Bounded retry on transient failure

Result delivery SHALL make at most three attempts — one immediate attempt plus two retries — and SHALL stop as soon as an attempt succeeds.

#### Scenario: Transient failure then success

- **WHEN** the first post attempt fails with a transient error and the next succeeds
- **THEN** the result is delivered and no further attempts are made

#### Scenario: Persistent transient failure exhausts attempts

- **WHEN** every attempt fails with a transient error
- **THEN** exactly three attempts are made and then delivery stops

### Requirement: Backoff between attempts, interruptible by stop

Delivery SHALL wait 1 second before the second attempt and 2 seconds before the third. The wait SHALL be interrupted immediately by the service stop signal, ending retries without waiting out the remaining backoff.

#### Scenario: Backoff precedes a retry

- **WHEN** an attempt fails transiently and another attempt remains
- **THEN** delivery waits the backoff for that attempt (1s, then 2s) before retrying

#### Scenario: Stop interrupts the backoff

- **WHEN** the service stop signal is set while delivery is in a backoff wait
- **THEN** delivery returns at once without completing the wait or making further attempts

### Requirement: Retryable versus non-retryable classification

Delivery SHALL retry transient failures — transport errors (`HttpRequestException`), HTTP `5xx` responses, and HTTP-timeout cancellations — and SHALL NOT retry HTTP `4xx` responses or serialization errors.

#### Scenario: Server error is retried

- **WHEN** a post attempt fails with an HTTP `5xx` response
- **THEN** delivery treats it as transient and retries within the attempt budget

#### Scenario: Client error is not retried

- **WHEN** a post attempt fails with an HTTP `4xx` response
- **THEN** delivery does not retry and stops after that single attempt

### Requirement: Stable payload across attempts

Every attempt SHALL send the same result payload — `taskId`, `taskType`, and `data` do not change between attempts.

#### Scenario: Same payload retried

- **WHEN** delivery retries after a transient failure
- **THEN** the retried request carries the identical result that the first attempt sent

### Requirement: Never throws; logs on failure to deliver

Delivery SHALL NOT propagate an exception to the polling loop. On a non-retryable error or on exhausting the attempts it SHALL log the failure, and on exhaustion the log SHALL include the `taskId`, the `taskType`, the number of attempts made, and that delivery was not confirmed.

#### Scenario: Exhaustion is logged and swallowed

- **WHEN** all attempts fail with a transient error
- **THEN** delivery returns without throwing and logs the `taskId`, `taskType`, attempt count, and that delivery was not confirmed

#### Scenario: Non-retryable error is logged and swallowed

- **WHEN** an attempt fails with a non-retryable error
- **THEN** delivery returns without throwing and logs the failure

