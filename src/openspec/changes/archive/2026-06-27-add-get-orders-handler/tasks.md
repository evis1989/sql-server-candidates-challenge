## 1. Extract the read primitive

- [x] 1.1 Add `Database/DbQuery.cs` — `static IReadOnlyList<object> ReadRows(IDbConnection openConnection, string sql, Action<IDbCommand> bindParameters, Func<IDataReader, object> mapRow)` (creates command, binds, executes reader, maps; does NOT open/dispose the connection); move `GetStringOrNull` here as a public static
- [x] 1.2 Refactor `SqlTaskHandler` to open the connection and delegate row reading to `DbQuery.ReadRows`; keep `GetStringOrNull` as a `protected static` forwarder so the simple handlers compile unchanged
- [x] 1.3 Run the suite — the 16 existing tests must stay green unchanged (regression guard for the extraction)

## 2. Models

- [x] 2.1 Add `Models/OrderDetailRecord.cs` — `productName`, `productNumber`, `unitPrice` (decimal), `quantity` (int), `lineTotal` (decimal); `[JsonProperty]` camelCase
- [x] 2.2 Add `Models/OrderRecord.cs` — `salesOrderId` (int), `orderDate` (DateTime), `status` (int), `customerName`, `accountNumber`, `totalDue` (decimal), `orderDetails` (List<OrderDetailRecord>, initialized empty); `[JsonProperty]` camelCase

## 3. Handler

- [x] 3.1 Add `Tasks/Handlers/GetOrdersHandler.cs` implementing `ITaskHandler`; ctor takes `IDbConnectionFactory`; `CanHandle` true only for `TaskTypes.GetOrders`
- [x] 3.2 Header query: const SQL joining `SalesOrderHeader` → `Customer` → `Person` (LEFT), filtered `soh.ModifiedDate >= @modifiedSince`; `MapHeader` composes `customerName` from first/last in C#, reads `status` via `Convert.ToInt32(GetValue)`, `totalDue`/`orderDate` typed
- [x] 3.3 Detail query: `BuildDetailSql(count)` produces `@id0..@id{N-1}`; SQL joins `SalesOrderDetail` → `Product`, `WHERE SalesOrderID IN (...)`; bind each id as a parameter; `MapDetail` returns a private `(orderId, OrderDetailRecord)` carrier with `quantity` via `Convert.ToInt32(GetValue)`
- [x] 3.4 `Execute`: open one connection; run header query; if zero headers return `Completed` with empty data (skip detail query); else run detail query, stitch details into headers by `salesOrderId` (dictionary, preserve header order); return `Completed`. Wrap all in try/catch → `Failed`

## 4. Registration

- [x] 4.1 In `Program.cs`, add `new GetOrdersHandler(dbFactory)` to the dispatcher array

## 5. Tests

- [x] 5.1 `Tasks/Handlers/GetOrdersHandlerTests.cs` — `CanHandle` true for `GetOrders`, false for the other three
- [x] 5.2 Stitch test: header reader (2 orders) + detail reader (lines across both) → two `OrderRecord`s each with only its own `orderDetails`; `recordCount == 2`; header + detail fields mapped (decimals, int status/quantity)
- [x] 5.3 Empty-headers test: header reader returns 0 rows → completed with empty data, `recordCount` 0, and the detail query is never executed (verify command/reader for the second query is not created)
- [x] 5.4 Parameter test: header query binds `@modifiedSince`; detail query binds one `@idN` parameter per header id with the right values
- [x] 5.5 Failure test: connection throws → `status` `failed`, `data` null, non-empty `errorMessage`

## 6. Verification

- [x] 6.1 `dotnet build` clean and `dotnet test` green (existing 16 + new orders tests; simple-handler tests unchanged)
- [x] 6.2 Note for submission: run once against a live AdventureWorks2025 to confirm the order/detail SQL maps the documented shape
