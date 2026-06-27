## Why

`GetProductInventory` is the fourth and final handler. Its output is a flat row per product-location inventory record, so it fits the single-query `SqlTaskHandler` template exactly — the same shape as `GetCustomers` and `GetProducts`. Adding it completes coverage of all four task types; after this the dispatcher routes every documented type to a real handler.

## What Changes

- Add `Models/InventoryRecord.cs` with the eight contract fields: `productId`, `productName`, `productNumber`, `locationName`, `shelf`, `bin`, `quantity`, `modifiedDate`.
- Add `Tasks/Handlers/GetProductInventoryHandler.cs` extending `SqlTaskHandler`: `TaskType` = `GetProductInventory`; SQL joining `Production.ProductInventory` → `Production.Product` → `Production.Location`, filtered `ProductInventory.ModifiedDate >= @modifiedSince`; `MapRow` reads `bin` (tinyint) and `quantity` (smallint) via `Convert.ToInt32` and the rest typed.
- Register `GetProductInventoryHandler` in the `TaskDispatcher` composition in `Program.cs`.
- Unit tests with a mocked reader: `CanHandle`, mapping (including `bin`/`quantity` from tinyint/smallint), parameter binding, and the failure path.

## Capabilities

### New Capabilities
- `get-product-inventory-sync`: executing a `GetProductInventory` task — the `modifiedSince`-filtered, parameterized query joining inventory with product and location, the row-to-contract mapping, and the completed/failed result behavior.

### Modified Capabilities

(none)

## Impact

- New code: `InventoryRecord`, `GetProductInventoryHandler`, and the handler test. Reuses the `SqlTaskHandler` base and `DbQuery` primitive — no new plumbing.
- One-line registration edit in `Program.cs`. No new dependencies.
- Each row is one product-location inventory record (`ProductInventory` PK is `ProductID` + `LocationID`), so a product in several locations yields several flat rows — correct for this contract.
- Runtime query targets AdventureWorks2025; SQL correctness still requires a live-database smoke test (tests use a mocked reader).
