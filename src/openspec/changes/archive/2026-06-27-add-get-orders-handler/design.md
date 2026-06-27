## Context

`SqlTaskHandler` is a single-query template: one SQL, one `MapRow`, one flat result. `GetOrders` breaks that — an order has a nested `orderDetails[]`, and the chosen approach is two queries (headers, then details by id) stitched in memory rather than de-duplicating a header×detail JOIN. So `GetOrders` cannot extend the template as-is. This is the growth point we deferred when the base was introduced. Shapes and the filter come from `openspec/project.md` and `docs/sample-payloads/result-get-orders.json`.

## Goals / Non-Goals

**Goals:**

- Two-query header/detail fetch with in-memory stitching, per the chosen approach (KISS, no header duplication, each query testable alone).
- Extract the shared read primitive so `GetOrders` reuses the same plumbing as the simple handlers without bending the template.
- Keep the simple handlers and their 16 tests untouched.
- Parameterized everything, including the dynamic `IN` list.

**Non-Goals:**

- No `GetProductInventory` yet.
- No single-JOIN-with-grouping alternative (explicitly rejected by the chosen approach).
- No live-database integration test.

## Decisions

- **Extract `Database/DbQuery` — the read primitive.** A static helper:
  `IReadOnlyList<object> ReadRows(IDbConnection openConnection, string sql, Action<IDbCommand> bindParameters, Func<IDataReader, object> mapRow)`.
  It creates the command, lets the caller bind parameters, executes the reader, and maps each row. It does **not** open or dispose the connection — the caller owns that, which is what lets `GetOrders` run two `ReadRows` calls on one open connection. The null-tolerant `GetStringOrNull` moves here too. `SqlTaskHandler` is refactored to: open connection → `DbQuery.ReadRows(conn, Sql, bind @modifiedSince, MapRow)`, and its `GetStringOrNull` forwards to `DbQuery`. Public/abstract surface of `SqlTaskHandler` is unchanged, so `GetCustomersHandler`/`GetProductsHandler` and their tests don't change — those 16 tests guard the refactor.
- **`GetOrdersHandler` implements `ITaskHandler` directly** (not the single-query base). Its `Execute`: open one connection, run the header query, short-circuit on empty, run the detail query, stitch, return `Completed`; wrap the whole thing in try/catch → `Failed`. The only things it re-declares vs the base are `CanHandle` (one line) and the try/catch+open wrapper (~5 lines) — cheaper than distorting the base to model two-query handlers.
- **Two queries, one connection.**
  - Header SQL: `Sales.SalesOrderHeader soh INNER JOIN Sales.Customer c ON c.CustomerID = soh.CustomerID LEFT JOIN Person.Person p ON p.BusinessEntityID = c.PersonID`, selecting `SalesOrderID, OrderDate, Status, c.AccountNumber, soh.TotalDue, p.FirstName, p.LastName`, filtered `soh.ModifiedDate >= @modifiedSince`, ordered by `SalesOrderID`.
  - Detail SQL: `Sales.SalesOrderDetail sod INNER JOIN Production.Product prod ON prod.ProductID = sod.ProductID`, selecting `SalesOrderID, prod.Name, prod.ProductNumber, sod.UnitPrice, sod.OrderQty, sod.LineTotal`, `WHERE sod.SalesOrderID IN (@id0, @id1, …)`, ordered by `SalesOrderID, SalesOrderDetailID`.
- **`customerName` composed in C#, not SQL.** `MapHeader` reads `FirstName`/`LastName` via `GetStringOrNull` and joins them (`"First Last"`, trimmed; null when both are null). This keeps null handling explicit and avoids `CONCAT` returning a stray space for store customers (no person).
- **Parameterized `IN` list, generated placeholders.** The detail SQL text is built for N ids as `@id0..@id{N-1}`; the *values* are bound as parameters. Generating parameter **names** into SQL is safe (not user input); ids are never concatenated. A `BuildInClause(count)` produces the placeholder list.
- **Empty-headers short-circuit.** If the header query returns zero rows, the handler skips the detail query entirely (an `IN ()` is invalid SQL) and returns a completed result with empty data, `recordCount` 0.
- **Integer columns read defensively.** `Status` is `tinyint` and `OrderQty` is `smallint` in AdventureWorks; both map to the contract's integers via `Convert.ToInt32(reader.GetValue(ordinal))`, which is type-agnostic across tinyint/smallint/int rather than risking an `IDataReader.GetInt32` cast error. `recordCount` counts orders, not detail lines.
- **Models.** `OrderRecord` (`salesOrderId`, `orderDate`, `status` int, `customerName`, `accountNumber`, `totalDue` decimal, `orderDetails` list initialized empty) and `OrderDetailRecord` (`productName`, `productNumber`, `unitPrice` decimal, `quantity` int, `lineTotal` decimal), `[JsonProperty]` camelCase. Stitch keyed by `salesOrderId` via a dictionary, preserving header order.

## Risks / Trade-offs

- **The base refactor could regress the simple handlers** → `SqlTaskHandler`'s surface is unchanged and it now delegates to `DbQuery`; the 16 existing tests run unmodified as the guard.
- **`IN` list size limits** → SQL Server caps parameters at 2100. A single `modifiedSince` window could in theory exceed that many orders. `ponytail:` not handled now — for the documented sample volumes it is irrelevant; if it ever matters, batch the ids or switch to a table-valued parameter. Noted, not built.
- **SQL correctness unverifiable without the live DB** → stitching, the empty-headers short-circuit, both queries' parameter binding, and the failure path are unit-tested against mocked readers; the real-schema check is a manual run against AdventureWorks2025, flagged in tasks.
- **`customerName` for store orders** → composed from the LEFT-joined person; store customers (no `PersonID`) yield null, consistent with the flat contract. Noted as an assumption.
