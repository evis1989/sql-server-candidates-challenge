## 1. Result model

- [x] 1.1 Add `Models/CustomerRecord.cs` — the eleven contract fields with `[JsonProperty]` camelCase names (`customerId`, `accountNumber`, `firstName`, `lastName`, `emailAddress`, `phone`, `addressLine1`, `city`, `stateProvince`, `postalCode`, `countryRegion`)

## 2. Handler

- [x] 2.1 Add `Tasks/Handlers/GetCustomersHandler.cs` — ctor takes `IDbConnectionFactory`; `CanHandle` returns true only for `TaskTypes.GetCustomers`
- [x] 2.2 Implement `Execute`: open connection, create command with the parameterized SQL (`@modifiedSince` from `task.Parameters.ModifiedSince`), read rows, map each to `CustomerRecord`, return `SyncResult.Completed`
- [x] 2.3 Define the SQL as a `const` joining `Sales.Customer` → `Person.Person`/`EmailAddress`/`PersonPhone`/address chain, filtered `Sales.Customer.ModifiedDate >= @modifiedSince` (outer joins for optional columns, first address per customer)
- [x] 2.4 Add a null-tolerant `GetStringOrNull` read helper for optional columns; wrap the body so any exception returns `SyncResult.Failed` (never throws)

## 3. Registration

- [x] 3.1 In `Program.cs`, register the handler: `new ITaskHandler[] { new GetCustomersHandler(dbFactory) }` and drop the `_ = dbFactory` discard

## 4. Tests

- [x] 4.1 `Tasks/Handlers/GetCustomersHandlerTests.cs` — `CanHandle` true for `GetCustomers`, false for the other three types
- [x] 4.2 Mapping test: a mocked `IDataReader` returning two rows yields a completed result with two `CustomerRecord`s mapped to the right fields and `recordCount == 2`
- [x] 4.3 Null-column test: a row with null email/phone/address maps those fields to null without throwing
- [x] 4.4 Parameter test: executing binds a `@modifiedSince` parameter carrying the task's value (assert via the mocked command's parameter collection)
- [x] 4.5 Failure test: when the factory/connection throws, `Execute` returns `status` `failed` with `data` null and a non-empty `errorMessage`

## 5. Verification

- [x] 5.1 `dotnet build` clean (0 warnings, 0 errors) and `dotnet test` green (existing 6 + new handler tests)
- [x] 5.2 Note for submission: run once against a live AdventureWorks2025 to confirm the SQL maps the documented shape (cannot be verified here without the database)
