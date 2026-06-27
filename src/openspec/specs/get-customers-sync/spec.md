# get-customers-sync Specification

## Purpose
TBD - created by archiving change add-get-customers-handler. Update Purpose after archive.
## Requirements
### Requirement: Handles the GetCustomers task type

The handler SHALL report that it can handle the `GetCustomers` task type and SHALL NOT claim any other type.

#### Scenario: Claims GetCustomers

- **WHEN** the dispatcher asks whether the handler can handle `GetCustomers`
- **THEN** the handler returns true

#### Scenario: Rejects other types

- **WHEN** the dispatcher asks whether the handler can handle any type other than `GetCustomers`
- **THEN** the handler returns false

### Requirement: Parameterized modifiedSince filter

The handler SHALL filter results by the task's `modifiedSince` value bound as a SQL parameter; it SHALL NOT build the query by string concatenation of that value.

#### Scenario: modifiedSince bound as a parameter

- **WHEN** the handler executes a `GetCustomers` task
- **THEN** the command carries `modifiedSince` as a command parameter and the SQL text contains no interpolated date literal

### Requirement: Maps rows to the customer contract shape

For each matched record the handler SHALL produce an object with exactly these fields: `customerId`, `accountNumber`, `firstName`, `lastName`, `emailAddress`, `phone`, `addressLine1`, `city`, `stateProvince`, `postalCode`, `countryRegion`.

#### Scenario: Row mapped to contract fields

- **WHEN** the query returns a customer row
- **THEN** the handler maps its columns to the eleven contract fields in the completed result's data

#### Scenario: Record count matches data

- **WHEN** the handler completes with N mapped rows
- **THEN** the result's `recordCount` equals N and `status` is `completed`

### Requirement: Failures return a failed result

The handler SHALL NOT throw out of `Execute`; on any error it SHALL return a failed `SyncResult` carrying the error message.

#### Scenario: Database error becomes a failed result

- **WHEN** opening the connection or executing the query throws
- **THEN** the handler returns a `SyncResult` with `status` `failed`, `data` null, and `errorMessage` set to the error description

