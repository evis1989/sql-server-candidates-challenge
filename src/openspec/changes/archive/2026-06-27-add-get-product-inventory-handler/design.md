## Context

The fourth handler. Unlike `GetOrders`, its output is flat — one row per product-location inventory record — so it slots straight into the `SqlTaskHandler` template (`TaskType`/`Sql`/`MapRow`) that `GetCustomers` and `GetProducts` already use, reusing the `DbQuery` primitive underneath. Shape and filter come from `openspec/project.md` and `docs/sample-payloads/result-get-product-inventory.json`. Nothing about the base or the plumbing changes.

## Goals / Non-Goals

**Goals:**

- A `GetProductInventoryHandler` on the existing base, plus an `InventoryRecord` model.
- Correct integer reads for the tinyint `bin` and smallint `quantity`.
- Complete coverage: all four task types routed to real handlers.

**Non-Goals:**

- No base or `DbQuery` changes — this handler needs nothing new.
- No live-database integration test.

## Decisions

- **Extend `SqlTaskHandler`.** Declares only `TaskType` (`GetProductInventory`), `Sql`, and `MapRow`. The base owns connection lifetime, `@modifiedSince` binding, the reader loop, and the completed/failed contract. This is the third single-query handler and confirms the base earns its keep.
- **`Models/InventoryRecord.cs` with typed fields.** `productId`/`bin`/`quantity` int; `modifiedDate` `DateTime`; `productName`/`productNumber`/`locationName`/`shelf` strings; `[JsonProperty]` camelCase matching the contract.
- **Defensive integer reads for `bin` and `quantity`.** `ProductInventory.Bin` is `tinyint` and `Quantity` is `smallint`; both map to the contract's integers via `Convert.ToInt32(reader.GetValue(ordinal))`, avoiding the `IDataReader.GetInt32` cast error on non-int storage — the same approach used in `GetOrders`. `productId` is a true `int` so it reads via `GetInt32`; `modifiedDate` via `GetDateTime`; `shelf`/`locationName`/`productName`/`productNumber` via `GetStringOrNull`.
- **SQL.** `Production.ProductInventory pi INNER JOIN Production.Product p ON p.ProductID = pi.ProductID INNER JOIN Production.Location l ON l.LocationID = pi.LocationID`, selecting `pi.ProductID, p.Name AS ProductName, p.ProductNumber, l.Name AS LocationName, pi.Shelf, pi.Bin, pi.Quantity, pi.ModifiedDate`, filtered `pi.ModifiedDate >= @modifiedSince`, ordered by `pi.ProductID, l.LocationID`. INNER joins: every inventory row has a product and a location.
- **Registration.** `Program.cs` dispatcher array gains `new GetProductInventoryHandler(dbFactory)`, completing the set of four.

## Risks / Trade-offs

- **SQL correctness unverifiable without the live DB** → mapping (incl. the tinyint/smallint integer reads), parameter binding, and the failure path are unit-tested against a mocked reader; the real-schema check is a manual run against AdventureWorks2025, flagged in tasks.
- **`shelf` storage** → `Shelf` is `nvarchar` in AW (values like "A"); read as a string. `bin` is the numeric one (tinyint). Kept straight by reading each with its matching accessor.
