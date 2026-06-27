## Context

The scaffold (`sync-agent-runtime`, `platform-client`) gave us the dispatcher seam and the `ITaskHandler` interface but no handlers. This change fills in the first one. `IDbConnectionFactory.CreateConnection()` already returns `System.Data.IDbConnection`, so a handler can be written entirely against ADO.NET interfaces and unit-tested with mocks — no live database needed for tests. The customer output shape and the `modifiedSince` filter come from `openspec/project.md` and the sample payload `docs/sample-payloads/result-get-customers.json`.

## Goals / Non-Goals

**Goals:**

- A `GetCustomersHandler` that is the reference implementation for the remaining three handlers.
- Fully parameterized SQL (`modifiedSince` as a `SqlParameter`), SQL text living in the handler.
- A typed `CustomerRecord` model that serializes to the exact contract field names.
- Unit tests (mocked connection/reader) covering row mapping, the parameter binding, and the failure path.

**Non-Goals:**

- No shared handler base class or query helper yet (see Decisions — deferred until the second handler).
- No changes to the loop, client, or the other three task types.
- No live-database integration test (can't run without AdventureWorks2025 present).

## Decisions

- **Code against ADO.NET interfaces, not `SqlConnection`.** The handler uses `IDbConnection` / `IDbCommand` / `IDataReader` obtained from the factory. This is what makes it unit-testable with Moq (mock `IDbConnectionFactory` → `IDbConnection` → `IDbCommand` → `IDataReader`) and keeps it provider-agnostic. The concrete `SqlConnection` is only ever created inside `DbConnectionFactory`.
- **Typed result model `Models/CustomerRecord.cs`.** One model per task result shape (per project.md), with `[JsonProperty]` camelCase names matching the contract. `SyncResult.Data` holds `IReadOnlyList<object>` of these. Alternative — `Dictionary<string,object>` per row — rejected; a typed model is self-documenting and the serialization is verifiable in a test.
- **Parameterized query, SQL as a `const` in the handler.** `modifiedSince` is bound via `command.CreateParameter()` (name `@modifiedSince`), never concatenated. The query joins `Sales.Customer` → `Person.Person` (PersonID) → `Person.EmailAddress`, `Person.PersonPhone`, and the customer's address chain (`Person.BusinessEntityAddress` → `Person.Address` → `Person.StateProvince` → `Person.CountryRegion`), filtered on `Sales.Customer.ModifiedDate >= @modifiedSince`. Outer joins for email/phone/address so customers missing those still return.
- **Null-tolerant column reads.** Email, phone, and address columns can be null; reads go through a small `GetStringOrNull(reader, ordinal)` rather than `reader.GetString`, which throws on `DBNull`. Tested with a row that has null optional columns.
- **No base class / query helper yet.** `ponytail:` a single handler doesn't justify an abstraction. The open→command→param→reader→map flow stays visible in this one file as the legible template. When the second handler lands and the boilerplate actually repeats, extract a `QueryList(sql, bindParams, mapRow)` helper or a base handler then — that's the upgrade path.
- **Registration in `Program.cs`.** Replace the empty `new ITaskHandler[0]` with `new ITaskHandler[] { new GetCustomersHandler(dbFactory) }`. The `dbFactory` already constructed there stops being unused.
- **First/primary address only.** A customer can have several addresses; the query takes one (the first by `AddressID`) to keep a single flat row per customer matching the contract. Noted as an assumption; revisit if the contract later wants all addresses.

## Risks / Trade-offs

- **SQL correctness is unverifiable without the live DB** → mapping, parameter binding, and the failure path are unit-tested against a mocked reader; the real-schema check is a manual run against AdventureWorks2025. The SQL is written from the documented AW schema and flagged as needing a live smoke test before submission.
- **Address/phone/email cardinality** → outer joins + "first address" keep one row per customer; if a customer truly has multiple emails/phones the query returns the first, matching the flat contract shape.
- **Establishing the wrong pattern is costly** (three handlers copy it) → keep the reference handler minimal and interface-driven so the copy is mechanical, and resist premature abstraction so handlers 2–4 inform the eventual shared helper rather than forcing it now.
