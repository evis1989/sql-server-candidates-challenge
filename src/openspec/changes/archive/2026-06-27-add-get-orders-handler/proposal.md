## Why

`GetOrders` is the third handler and the first whose output is not a flat row: each order carries a nested `orderDetails[]`. It does not fit the single-query `SqlTaskHandler` template (`TaskType`/`Sql`/`MapRow`), so this is the point — flagged when the base was introduced — where the shared plumbing grows to serve a handler that needs more than one query.

## What Changes

- Extract the reusable read primitive — create command, bind parameters, `ExecuteReader`, iterate and map rows — out of `SqlTaskHandler` into a shared helper (`Database/DbQuery`), so both the single-query handlers and `GetOrders` use the same plumbing. `SqlTaskHandler`'s public/abstract surface and the simple handlers stay unchanged; their tests are the regression guard.
- Add `GetOrdersHandler` implementing `ITaskHandler` with the user's two-query approach:
  - Query 1: order headers from `Sales.SalesOrderHeader` filtered `ModifiedDate >= @modifiedSince`.
  - Query 2: order details from `Sales.SalesOrderDetail` for the header `SalesOrderID`s, `WHERE SalesOrderID IN (@id0, @id1, …)`.
  - Stitch: group details by `SalesOrderID` into each header's `orderDetails`.
- The `IN (…)` list is parameterized dynamically (`@id0`, `@id1`, …) — ids bound as parameters, never concatenated.
- Edge case: zero headers → skip the second query entirely (never emit an invalid `IN ()`); return a completed result with empty data.
- Add models `OrderRecord` (header + `orderDetails[]`) and `OrderDetailRecord`.
- Register `GetOrdersHandler` in the `TaskDispatcher` composition in `Program.cs`.
- Unit tests with mocked readers per query: header+detail stitching, the empty-headers short-circuit, parameter binding for both queries, and the failure path.
- No changes to `GetProductInventory`.

## Capabilities

### New Capabilities
- `get-orders-sync`: executing a `GetOrders` task — the two-query header/detail fetch, the `modifiedSince` filter, the parameterized `IN` detail lookup, the stitch into nested `orderDetails`, and the completed/failed result behavior.

### Modified Capabilities

(none — extracting the read primitive out of `SqlTaskHandler` is an internal refactor; `get-customers-sync` and `get-products-sync` behavior is unchanged)

## Impact

- New code: `Database/DbQuery`, `GetOrdersHandler`, `OrderRecord`, `OrderDetailRecord`, and the orders handler test.
- Refactor: `SqlTaskHandler` delegates its row reading to `DbQuery` (behavior identical; the 16 existing tests guard it).
- One-line registration edit in `Program.cs`. No new dependencies.
- Runtime queries target AdventureWorks2025; SQL correctness still requires a live-database smoke test (tests use mocked readers).
