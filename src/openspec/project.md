# SQL Server Sync Agent — Kabilio Technical Challenge

## Overview

An always-on Windows Service that polls a central platform API for sync tasks,
queries a local SQL Server (AdventureWorks2025), and posts results back.
This simulates the real-world architecture used in Kabilio's production system.

## Architecture Decision

**Windows Service on .NET Framework 4.8** — chosen deliberately to match the
actual technology stack described in the Kabilio job description. This is not
a greenfield .NET 8 project; the goal is to demonstrate deep familiarity with
the Windows Service lifecycle, installation, startup behavior, and recovery
options — exactly what the role requires.

## Stack

- Runtime: .NET Framework 4.8
- Application type: Windows Service (System.ServiceProcess.ServiceBase)
- Database: SQL Server (AdventureWorks2025 OLTP sample database)
- HTTP client: System.Net.Http.HttpClient
- Serialization: Newtonsoft.Json
- Testing: NUnit + Moq
- Installer: not in scope for the challenge (but would use WiX/MSI in production)

## Engineering Principles

- **Clean Code**: readable, self-documenting, no clever tricks
- **SOLID**: each class has one responsibility; dependencies are injected via interfaces
- **DRY**: shared logic lives in one place — especially query building and HTTP communication
- **KISS**: no over-engineering; the simplest solution that is correct and maintainable
- **Test everything important**: task handlers, query builders, HTTP client wrapper, result mapper

## Project Structure (candidate/)

```
candidate/
  SyncAgent/
    SyncAgent.sln
    SyncAgent/
      Program.cs                  ← entry point; installs or runs the service
      SyncAgentService.cs         ← Windows Service lifecycle (OnStart / OnStop)
      SyncLoop.cs                 ← polling loop; orchestrates task fetch → execute → post
      Configuration/
        AppSettings.cs            ← strongly-typed config loaded from App.config
      Http/
        ISyncPlatformClient.cs    ← interface for platform API calls
        SyncPlatformClient.cs     ← HttpClient wrapper; GET next-task, POST result
      Tasks/
        ITaskHandler.cs           ← interface: bool CanHandle(taskType), SyncResult Execute(task)
        TaskDispatcher.cs         ← finds the right handler for a given task type
        Handlers/
          GetCustomersHandler.cs
          GetProductsHandler.cs
          GetOrdersHandler.cs
          GetProductInventoryHandler.cs
      Database/
        IDbConnectionFactory.cs   ← interface for creating SqlConnection
        DbConnectionFactory.cs
      Models/
        SyncTask.cs               ← deserialized task from GET /next-task
        SyncResult.cs             ← payload sent to POST /result
        TaskParameters.cs
        (one model per task result shape)
    SyncAgent.Tests/
      Http/
        SyncPlatformClientTests.cs
      Tasks/
        TaskDispatcherTests.cs
        Handlers/
          GetCustomersHandlerTests.cs
          GetProductsHandlerTests.cs
          GetOrdersHandlerTests.cs
          GetProductInventoryHandlerTests.cs
```

## API Contract

**Base URL:** `http://localhost:5100`
**Auth header:** `X-Api-Key: candidate-test-key-2026`

### GET /api/sync/next-task
- 200 → task available (JSON body with taskId, taskType, parameters.modifiedSince, createdAt)
- 204 → no tasks queued; agent sleeps and retries after polling interval

### POST /api/sync/result
- Body: taskId, taskType, status ("completed"/"failed"), data (array or null),
  recordCount, executedAt (ISO 8601), errorMessage (null or string)
- 200 → accepted

## Task Types and SQL Mapping (AdventureWorks2025)

### GetCustomers
Filters by modifiedSince on Person.Person or Sales.Customer.ModifiedDate.
Returns: customerId, accountNumber, firstName, lastName, emailAddress, phone,
addressLine1, city, stateProvince, postalCode, countryRegion.

### GetProducts
Filters by Production.Product.ModifiedDate >= modifiedSince.
Returns: productId, name, productNumber, color, standardCost, listPrice,
category (ProductCategory.Name), subcategory (ProductSubcategory.Name), modifiedDate.

### GetOrders
Filters by Sales.SalesOrderHeader.ModifiedDate >= modifiedSince.
Returns: salesOrderId, orderDate, status, customerName, accountNumber, totalDue,
orderDetails[] (productName, productNumber, unitPrice, quantity, lineTotal).

### GetProductInventory
Filters by Production.ProductInventory.ModifiedDate >= modifiedSince.
Returns: productId, productName, productNumber, locationName, shelf, bin,
quantity, modifiedDate.

## Polling Behavior

- Interval: configurable via App.config (default 5 seconds)
- On 204: sleep and retry
- On task received: execute immediately, post result, then poll again
- On error: post failed result with errorMessage, log, continue polling (do not crash)
- On unhandled exception: log and continue; Windows Service recovery handles restart

## Security

- API key loaded from App.config (not hardcoded)
- SQL queries use parameterized queries exclusively (no string concatenation)
- No sensitive data logged

## Configuration (App.config keys)

- `PlatformBaseUrl` → http://localhost:5100
- `PlatformApiKey` → candidate-test-key-2026
- `DatabaseConnectionString` → connection to AdventureWorks2025
- `PollingIntervalSeconds` → 5

## Conventions

- All public types have XML doc comments
- No magic strings — use constants or enums for task type names
- All SQL is in the handler class, not inline in the service loop
- Interfaces for everything that touches I/O (HTTP, DB) — makes unit testing clean
- Tests mock the interface, not the concrete class
