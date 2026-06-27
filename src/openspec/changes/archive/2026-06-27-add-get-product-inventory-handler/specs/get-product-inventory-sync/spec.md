## ADDED Requirements

### Requirement: Handles the GetProductInventory task type

The handler SHALL report that it can handle the `GetProductInventory` task type and SHALL NOT claim any other type.

#### Scenario: Claims GetProductInventory

- **WHEN** the dispatcher asks whether the handler can handle `GetProductInventory`
- **THEN** the handler returns true

#### Scenario: Rejects other types

- **WHEN** the dispatcher asks whether the handler can handle any type other than `GetProductInventory`
- **THEN** the handler returns false

### Requirement: Parameterized modifiedSince filter

The handler SHALL filter results by the task's `modifiedSince` value bound as a SQL parameter; it SHALL NOT build the query by string concatenation of that value.

#### Scenario: modifiedSince bound as a parameter

- **WHEN** the handler executes a `GetProductInventory` task
- **THEN** the command carries `modifiedSince` as a command parameter and the SQL text contains no interpolated date literal

### Requirement: Maps rows to the inventory contract shape

For each matched record the handler SHALL produce an object with exactly these fields: `productId`, `productName`, `productNumber`, `locationName`, `shelf`, `bin`, `quantity`, `modifiedDate`. `bin` and `quantity` SHALL be integers and `modifiedDate` a timestamp.

#### Scenario: Row mapped to contract fields

- **WHEN** the query returns an inventory row
- **THEN** the handler maps its columns to the eight contract fields, reading `bin` and `quantity` as integers even though they are stored as tinyint/smallint

#### Scenario: One row per product-location

- **WHEN** a product has inventory in several locations
- **THEN** the result contains one flat row per product-location record

#### Scenario: Record count matches data

- **WHEN** the handler completes with N mapped rows
- **THEN** the result's `recordCount` equals N and `status` is `completed`

### Requirement: Failures return a failed result

The handler SHALL NOT throw out of `Execute`; on any error it SHALL return a failed `SyncResult` carrying the error message.

#### Scenario: Database error becomes a failed result

- **WHEN** opening the connection or executing the query throws
- **THEN** the handler returns a `SyncResult` with `status` `failed`, `data` null, and `errorMessage` set to the error description
