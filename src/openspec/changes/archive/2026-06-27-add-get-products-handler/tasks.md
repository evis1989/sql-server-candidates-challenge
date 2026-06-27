## 1. Shared base

- [x] 1.1 Add `Tasks/Handlers/SqlTaskHandler.cs` — abstract `ITaskHandler`: ctor takes `IDbConnectionFactory`; `CanHandle` matches abstract `TaskType`; `Execute` wraps query in try → `Completed` / catch → `Failed`; private `Query(modifiedSince)` runs the open→command→`@modifiedSince` param→reader→`MapRow` loop; `protected abstract string TaskType`, `string Sql`, `object MapRow(IDataReader)`; `protected static GetStringOrNull`

## 2. Refactor GetCustomers onto the base

- [x] 2.1 Rewrite `GetCustomersHandler` to extend `SqlTaskHandler` — keep the same SQL and the same column mapping; remove the now-duplicated plumbing and the local `GetStringOrNull`
- [x] 2.2 Run the existing customer tests — they must stay green unchanged (regression guard for the refactor)

## 3. Product model and handler

- [x] 3.1 Add `Models/ProductRecord.cs` — `productId` (int), `name`, `productNumber`, `color`, `standardCost` (decimal), `listPrice` (decimal), `category`, `subcategory`, `modifiedDate` (DateTime); `[JsonProperty]` camelCase names
- [x] 3.2 Add `Tasks/Handlers/GetProductsHandler.cs` extending `SqlTaskHandler` — `TaskType` = `GetProducts`; SQL joining `Production.Product` → `ProductSubcategory` → `ProductCategory` (LEFT), filtered `p.ModifiedDate >= @modifiedSince`
- [x] 3.3 Implement `MapRow`: inline `GetInt32`/`GetDecimal`/`GetDateTime` for non-null columns, `GetStringOrNull` for `color`/`category`/`subcategory`

## 4. Registration

- [x] 4.1 In `Program.cs`, add `new GetProductsHandler(dbFactory)` to the dispatcher array alongside `GetCustomersHandler`

## 5. Tests

- [x] 5.1 `Tasks/Handlers/GetProductsHandlerTests.cs` — `CanHandle` true for `GetProducts`, false for the other three types
- [x] 5.2 Mapping test: a mocked `IDataReader` with two rows yields two `ProductRecord`s with correct decimal `standardCost`/`listPrice` and `DateTime` `modifiedDate`, `recordCount == 2`
- [x] 5.3 Null-subcategory test: a row with null `color`/`category`/`subcategory` maps those to null without throwing
- [x] 5.4 Parameter test: executing binds a `@modifiedSince` parameter carrying the task's value
- [x] 5.5 Failure test: when the connection throws, `Execute` returns `status` `failed`, `data` null, non-empty `errorMessage`

## 6. Verification

- [x] 6.1 `dotnet build` clean and `dotnet test` green (existing 11 + new product tests; customer tests unchanged)
- [x] 6.2 Note for submission: run once against a live AdventureWorks2025 to confirm the product SQL maps the documented shape
