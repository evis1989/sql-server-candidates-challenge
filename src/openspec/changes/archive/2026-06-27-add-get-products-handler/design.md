## Context

`GetCustomersHandler` was written self-contained on purpose: with one handler there was nothing to share. Now `GetProducts` is the second handler and its plumbing — open connection, create command, bind `@modifiedSince`, execute reader, iterate rows — is identical to `GetCustomers`. The only per-handler differences are the task type, the SQL text, and how a row maps to a record. That is the duplication `ponytail` waited for; this change extracts it. Output shape and filter come from `openspec/project.md` and `docs/sample-payloads/result-get-products.json`.

## Goals / Non-Goals

**Goals:**

- Extract an abstract `SqlTaskHandler` base that owns the shared flow; subclasses declare only task type, SQL, and row mapping.
- Migrate `GetCustomersHandler` onto the base with zero behavior change (its tests stay green).
- Add `GetProductsHandler` + `ProductRecord` on the base, handling decimals and a date.

**Non-Goals:**

- No changes to `GetOrders` / `GetProductInventory` (they adopt the base when written).
- No live-database integration test.
- No generic query DSL or repository layer — the base is the minimum that removes the duplication.

## Decisions

- **Template-method base `SqlTaskHandler : ITaskHandler`.** It holds the `IDbConnectionFactory`, implements `CanHandle` (`taskType == TaskType`) and `Execute` (try → `Completed`, catch → `Failed`), and runs the connection/command/parameter/reader loop. Subclasses override three members: `protected abstract string TaskType`, `protected abstract string Sql`, `protected abstract object MapRow(IDataReader)`. The null-tolerant `GetStringOrNull` moves onto the base as a `protected static` helper. This is now justified — two of four handlers, soon four — so it is no longer "a base class for one implementation."
- **Refactor `GetCustomersHandler` onto the base in this change.** Otherwise the duplication we just named would still exist. The handler shrinks to its task type, its SQL, and a `MapRow`. Its existing five tests are the regression guard — they must stay green unchanged.
- **`Models/ProductRecord.cs` with typed fields.** `productId` int; `standardCost`/`listPrice` `decimal` (SQL `money`); `modifiedDate` `DateTime`; `name`/`productNumber` non-null strings; `color`/`category`/`subcategory` nullable strings. `[JsonProperty]` camelCase names match the contract.
- **Typed non-null reads inline, only nullable strings via the helper.** `standardCost`/`listPrice`/`modifiedDate`/`productId` are NOT NULL in `Production.Product`, so they read directly (`GetDecimal`/`GetDateTime`/`GetInt32`). `color`, and the LEFT-joined `category`/`subcategory`, go through `GetStringOrNull`. No speculative typed-nullable helpers — add them when a handler actually needs a nullable decimal/date.
- **SQL.** `Production.Product p LEFT JOIN Production.ProductSubcategory ps ON ps.ProductSubcategoryID = p.ProductSubcategoryID LEFT JOIN Production.ProductCategory pc ON pc.ProductCategoryID = ps.ProductCategoryID`, selecting `ProductID, Name, ProductNumber, Color, StandardCost, ListPrice, pc.Name AS Category, ps.Name AS Subcategory, p.ModifiedDate`, filtered `p.ModifiedDate >= @modifiedSince`, ordered by `ProductID`. LEFT joins so a product with no subcategory (hence no category) still returns with nulls.
- **Registration.** `Program.cs` dispatcher array becomes `{ new GetCustomersHandler(dbFactory), new GetProductsHandler(dbFactory) }`.

## Risks / Trade-offs

- **The refactor could silently change `GetCustomers` behavior** → the five existing customer tests run unchanged against the refactored handler; if any behavior shifts, they fail. That is the whole point of having written them first.
- **SQL correctness unverifiable without the live DB** → mapping (incl. decimal/date), the null-subcategory path, parameter binding, and failure are unit-tested against a mocked reader; the real-schema check is a manual run against AdventureWorks2025, flagged in tasks.
- **Over-extracting the base** → kept to exactly the three abstract members + one helper. If the remaining two handlers turn out to need more (e.g. parameters beyond `modifiedSince`), the base grows then, informed by real need rather than guessed.
