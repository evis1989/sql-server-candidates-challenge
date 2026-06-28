# get-orders-sync Specification

## Purpose
TBD - created by archiving change add-get-orders-handler. Update Purpose after archive.
## Requirements
### Requirement: Handles the GetOrders task type

The handler SHALL report that it can handle the `GetOrders` task type and SHALL NOT claim any other type.

#### Scenario: Claims GetOrders

- **WHEN** the dispatcher asks whether the handler can handle `GetOrders`
- **THEN** the handler returns true

#### Scenario: Rejects other types

- **WHEN** the dispatcher asks whether the handler can handle any type other than `GetOrders`
- **THEN** the handler returns false

### Requirement: Parameterized modifiedSince filter

The handler SHALL filter order headers by the task's `modifiedSince` value bound as a SQL parameter; it SHALL NOT build the query by string concatenation of that value.

#### Scenario: modifiedSince bound as a parameter

- **WHEN** the handler executes a `GetOrders` task
- **THEN** the header query carries `modifiedSince` as a command parameter and the SQL text contains no interpolated date literal

### Requirement: Two-query header and detail fetch

The handler SHALL fetch order headers and order details in two separate queries, then nest each order's details under it; the detail query SHALL look up details by the header ids using a parameterized `IN` list, never string-concatenated ids.

#### Scenario: Details nested under their order

- **WHEN** the header query returns orders and the detail query returns their line items
- **THEN** each order in the completed result carries an `orderDetails` array containing only the details whose `salesOrderId` matches that order

#### Scenario: IN list is parameterized

- **WHEN** the handler runs the detail query for N header ids
- **THEN** the ids are bound as N command parameters and the SQL text contains no interpolated id literal

#### Scenario: No matching orders

- **WHEN** the header query returns zero orders
- **THEN** the handler does not run the detail query and returns a completed result with empty data and `recordCount` 0

### Requirement: Maps orders to the contract shape

For each order the handler SHALL produce an object with `salesOrderId`, `orderDate`, `status`, `customerName`, `accountNumber`, `totalDue`, and an `orderDetails` array whose items carry `productName`, `productNumber`, `unitPrice`, `quantity`, and `lineTotal`.

#### Scenario: Order and detail fields mapped

- **WHEN** the queries return an order with line items
- **THEN** the order's header fields and each detail's five fields are mapped to the contract shape, with numeric `totalDue`/`unitPrice`/`lineTotal` and integer `status`/`quantity`

#### Scenario: Record count counts orders

- **WHEN** the handler completes with N orders
- **THEN** the result's `recordCount` equals N (orders, not detail lines) and `status` is `completed`

### Requirement: Failures return a failed result

The handler SHALL NOT throw out of `Execute`; on any error it SHALL return a failed `SyncResult` carrying the error message.

#### Scenario: Database error becomes a failed result

- **WHEN** opening the connection or executing either query throws
- **THEN** the handler returns a `SyncResult` with `status` `failed`, `data` null, and `errorMessage` set to the error description

### Requirement: Detail lookup is batched under the parameter cap

The detail query SHALL look up its order ids in batches small enough to stay under the database's command parameter limit, so a header window of any size succeeds. Details from every batch SHALL be stitched into their orders, and a small result set SHALL still use a single batch.

#### Scenario: Large id set is split into batches

- **WHEN** the header query returns more orders than the batch size
- **THEN** the detail query runs once per batch, each binding only that batch's ids, and every batch's details are nested under their orders

#### Scenario: Small id set uses a single batch

- **WHEN** the header query returns fewer orders than the batch size
- **THEN** the detail query runs exactly once for all of them

