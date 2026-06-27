## Why

`GetCustomers` established the handler pattern. `GetProducts` is the second handler, which is exactly the point where the shared plumbing (open connection → create command → bind `modifiedSince` → execute reader → iterate) has now repeated and should be extracted — the refactor `ponytail` deferred until a second handler proved the duplication real. Doing it now keeps `GetProducts` (and the two remaining handlers) down to just their SQL and column mapping.

## What Changes

- Add a small abstract base `Tasks/Handlers/SqlTaskHandler` that owns the common flow: `CanHandle` (matches its task type), `Execute` (try → completed / catch → failed), and the connection/command/parameter/reader plumbing. Subclasses declare only their task type, SQL, and row mapping. Null-tolerant read helpers (`GetStringOrNull`, plus typed reads) live on the base.
- Refactor `GetCustomersHandler` onto the base — behavior unchanged, just removes the duplicated plumbing.
- Add `Tasks/Handlers/GetProductsHandler.cs` extending the base: SQL joining `Production.Product` → `ProductSubcategory` → `ProductCategory` (LEFT — a product may have no subcategory), filtered `Production.Product.ModifiedDate >= @modifiedSince`, mapping to the product output shape.
- Add `Models/ProductRecord.cs` with the nine contract fields, including decimal `standardCost`/`listPrice` and a `DateTime` `modifiedDate`.
- Register `GetProductsHandler` in the `TaskDispatcher` composition in `Program.cs` alongside `GetCustomersHandler`.
- Unit tests for `GetProductsHandler` with a mocked reader: mapping (incl. decimals and the date), null subcategory, parameter binding, and the failure path.
- No changes to `GetOrders` or `GetProductInventory`.

## Capabilities

### New Capabilities
- `get-products-sync`: executing a `GetProducts` task — the `modifiedSince`-filtered, parameterized query against `Production.Product` (with category/subcategory), the row-to-contract mapping, and the completed/failed result behavior.

### Modified Capabilities

(none — `get-customers-sync` behavior is unchanged; moving `GetCustomersHandler` onto the shared base is an internal refactor with no spec-level effect)

## Impact

- New code: `SqlTaskHandler` base, `GetProductsHandler`, `ProductRecord`, and the product handler test.
- Refactor: `GetCustomersHandler` slims down onto the base (its existing tests must stay green, guarding the refactor).
- One-line registration edit in `Program.cs`. No new dependencies.
- Runtime query targets AdventureWorks2025; SQL correctness still requires a live-database smoke test (tests use a mocked reader).
