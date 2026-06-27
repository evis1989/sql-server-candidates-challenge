## Why

The scaffold has an empty dispatcher and no handlers, so every task currently fails soft. `GetCustomers` is the simplest of the four task types, so implementing it first lets us establish the handler pattern — query, parameterization, row mapping, error handling, and test approach — that `GetProducts`, `GetOrders`, and `GetProductInventory` will copy.

## What Changes

- Add `Tasks/Handlers/GetCustomersHandler.cs` implementing `ITaskHandler`: `CanHandle` matches `GetCustomers`; `Execute` opens a connection via `IDbConnectionFactory`, runs a `modifiedSince`-parameterized query against AdventureWorks2025, maps rows to the customer output shape, and returns `SyncResult.Completed`. Any error returns `SyncResult.Failed` with the message.
- The query is fully parameterized — `modifiedSince` passed as a `SqlParameter`, never string-concatenated.
- Map each row to the contract shape: `customerId`, `accountNumber`, `firstName`, `lastName`, `emailAddress`, `phone`, `addressLine1`, `city`, `stateProvince`, `postalCode`, `countryRegion`.
- Register `GetCustomersHandler` in the `TaskDispatcher` composition in `Program.cs`.
- Add unit tests with the connection/factory mocked: verify row mapping and that `modifiedSince` is bound as a parameter. No real database in tests.
- No changes to the other three task types.

## Capabilities

### New Capabilities
- `get-customers-sync`: executing a `GetCustomers` task — the `modifiedSince`-filtered, parameterized query against AdventureWorks2025, the row-to-contract mapping, and the completed/failed result behavior.

### Modified Capabilities

(none — the dispatcher routing requirement in `sync-agent-runtime` is unchanged; this change registers a concrete handler into the existing seam)

## Impact

- New code: `Tasks/Handlers/GetCustomersHandler.cs` and its test; one-line registration edit in `Program.cs`.
- Reuses existing seams (`IDbConnectionFactory`, `ITaskHandler`, `SyncResult`, `TaskTypes`) — no new dependencies.
- Runtime query targets AdventureWorks2025; correctness against the live schema can only be confirmed where that database is available (tests use a mocked connection).
