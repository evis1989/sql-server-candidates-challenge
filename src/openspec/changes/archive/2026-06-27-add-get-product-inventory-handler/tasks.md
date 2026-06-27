## 1. Model

- [x] 1.1 Add `Models/InventoryRecord.cs` — `productId` (int), `productName`, `productNumber`, `locationName`, `shelf`, `bin` (int), `quantity` (int), `modifiedDate` (DateTime); `[JsonProperty]` camelCase names

## 2. Handler

- [x] 2.1 Add `Tasks/Handlers/GetProductInventoryHandler.cs` extending `SqlTaskHandler` — `TaskType` = `GetProductInventory`
- [x] 2.2 Define the SQL as a `const` joining `Production.ProductInventory` → `Production.Product` → `Production.Location` (INNER), filtered `pi.ModifiedDate >= @modifiedSince`, ordered by `pi.ProductID, l.LocationID`
- [x] 2.3 Implement `MapRow`: `GetInt32` for `productId`, `GetStringOrNull` for `productName`/`productNumber`/`locationName`/`shelf`, `Convert.ToInt32(GetValue)` for `bin` (tinyint) and `quantity` (smallint), `GetDateTime` for `modifiedDate`

## 3. Registration

- [x] 3.1 In `Program.cs`, add `new GetProductInventoryHandler(dbFactory)` to the dispatcher array (completing all four task types)

## 4. Tests

- [x] 4.1 `Tasks/Handlers/GetProductInventoryHandlerTests.cs` — `CanHandle` true for `GetProductInventory`, false for the other three types
- [x] 4.2 Mapping test: a mocked `IDataReader` with two rows yields two `InventoryRecord`s with correct fields, `bin`/`quantity` read from tinyint/smallint values, `recordCount == 2`
- [x] 4.3 Parameter test: executing binds a `@modifiedSince` parameter carrying the task's value
- [x] 4.4 Failure test: when the connection throws, `Execute` returns `status` `failed`, `data` null, non-empty `errorMessage`

## 5. Verification

- [x] 5.1 `dotnet build` clean and `dotnet test` green (existing 21 + new inventory tests)
- [x] 5.2 Note for submission: run once against a live AdventureWorks2025 to confirm the inventory SQL maps the documented shape
