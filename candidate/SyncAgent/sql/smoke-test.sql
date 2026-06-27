/* ============================================================================
   SyncAgent — smoke test for the four task-type queries against AdventureWorks2025
   ----------------------------------------------------------------------------
   The unit tests mock the data reader, so they verify mapping/parameterization
   but NOT that the SQL actually runs against the live schema. Run this once
   against a real AdventureWorks2025 before submitting to confirm each query
   resolves and returns the documented shape.

   Each query below is the exact SQL the matching handler runs (same joins,
   same @modifiedSince filter). For each task it prints a row count and a small
   sample. Adjust @modifiedSince to match the data window you expect rows in.

   How to run:
     SSMS  : open this file, set the database to AdventureWorks2025, Execute.
     sqlcmd: sqlcmd -S localhost -d AdventureWorks2025 -E -i smoke-test.sql
   ============================================================================ */

USE AdventureWorks2025;
GO

DECLARE @modifiedSince datetime2 = '2025-01-01T00:00:00';

/* ---------------------------------------------------------------------------
   1. GetCustomers
   --------------------------------------------------------------------------- */
PRINT '=== GetCustomers ===';

SELECT COUNT(*) AS CustomerRowCount
FROM Sales.Customer c
INNER JOIN Person.Person p ON p.BusinessEntityID = c.PersonID
WHERE c.ModifiedDate >= @modifiedSince;

SELECT TOP (10)
    c.CustomerID,
    c.AccountNumber,
    p.FirstName,
    p.LastName,
    em.EmailAddress,
    ph.PhoneNumber   AS Phone,
    a.AddressLine1,
    a.City,
    sp.Name          AS StateProvince,
    a.PostalCode,
    cr.Name          AS CountryRegion
FROM Sales.Customer c
INNER JOIN Person.Person p
    ON p.BusinessEntityID = c.PersonID
OUTER APPLY (
    SELECT TOP 1 e.EmailAddress
    FROM Person.EmailAddress e
    WHERE e.BusinessEntityID = p.BusinessEntityID
    ORDER BY e.EmailAddressID
) em
OUTER APPLY (
    SELECT TOP 1 pp.PhoneNumber
    FROM Person.PersonPhone pp
    WHERE pp.BusinessEntityID = p.BusinessEntityID
    ORDER BY pp.PhoneNumberTypeID
) ph
OUTER APPLY (
    SELECT TOP 1 bea.AddressID
    FROM Person.BusinessEntityAddress bea
    WHERE bea.BusinessEntityID = p.BusinessEntityID
    ORDER BY bea.AddressID
) addr
LEFT JOIN Person.Address a          ON a.AddressID = addr.AddressID
LEFT JOIN Person.StateProvince sp   ON sp.StateProvinceID = a.StateProvinceID
LEFT JOIN Person.CountryRegion cr   ON cr.CountryRegionCode = sp.CountryRegionCode
WHERE c.ModifiedDate >= @modifiedSince
ORDER BY c.CustomerID;

/* ---------------------------------------------------------------------------
   2. GetProducts
   --------------------------------------------------------------------------- */
PRINT '=== GetProducts ===';

SELECT COUNT(*) AS ProductRowCount
FROM Production.Product p
WHERE p.ModifiedDate >= @modifiedSince;

SELECT TOP (10)
    p.ProductID,
    p.Name,
    p.ProductNumber,
    p.Color,
    p.StandardCost,
    p.ListPrice,
    pc.Name AS Category,
    ps.Name AS Subcategory,
    p.ModifiedDate
FROM Production.Product p
LEFT JOIN Production.ProductSubcategory ps  ON ps.ProductSubcategoryID = p.ProductSubcategoryID
LEFT JOIN Production.ProductCategory pc     ON pc.ProductCategoryID = ps.ProductCategoryID
WHERE p.ModifiedDate >= @modifiedSince
ORDER BY p.ProductID;

/* ---------------------------------------------------------------------------
   3. GetOrders — two queries: headers, then details for those header ids.
      (The handler binds the header ids as @id0..@idN parameters; here the IN
      list is the same set of ids selected from the header window.)
   --------------------------------------------------------------------------- */
PRINT '=== GetOrders (headers) ===';

SELECT COUNT(*) AS OrderHeaderRowCount
FROM Sales.SalesOrderHeader soh
WHERE soh.ModifiedDate >= @modifiedSince;

SELECT TOP (10)
    soh.SalesOrderID,
    soh.OrderDate,
    soh.Status,
    p.FirstName,
    p.LastName,
    c.AccountNumber,
    soh.TotalDue
FROM Sales.SalesOrderHeader soh
INNER JOIN Sales.Customer c     ON c.CustomerID = soh.CustomerID
LEFT JOIN Person.Person p       ON p.BusinessEntityID = c.PersonID
WHERE soh.ModifiedDate >= @modifiedSince
ORDER BY soh.SalesOrderID;

PRINT '=== GetOrders (details for the header window) ===';

SELECT TOP (10)
    sod.SalesOrderID,
    prod.Name AS ProductName,
    prod.ProductNumber,
    sod.UnitPrice,
    sod.OrderQty,
    sod.LineTotal
FROM Sales.SalesOrderDetail sod
INNER JOIN Production.Product prod ON prod.ProductID = sod.ProductID
WHERE sod.SalesOrderID IN (
    SELECT soh.SalesOrderID
    FROM Sales.SalesOrderHeader soh
    WHERE soh.ModifiedDate >= @modifiedSince
)
ORDER BY sod.SalesOrderID, sod.SalesOrderDetailID;

/* ---------------------------------------------------------------------------
   4. GetProductInventory
   --------------------------------------------------------------------------- */
PRINT '=== GetProductInventory ===';

SELECT COUNT(*) AS InventoryRowCount
FROM Production.ProductInventory pi
WHERE pi.ModifiedDate >= @modifiedSince;

SELECT TOP (10)
    pi.ProductID,
    p.Name AS ProductName,
    p.ProductNumber,
    l.Name AS LocationName,
    pi.Shelf,
    pi.Bin,
    pi.Quantity,
    pi.ModifiedDate
FROM Production.ProductInventory pi
INNER JOIN Production.Product p  ON p.ProductID = pi.ProductID
INNER JOIN Production.Location l ON l.LocationID = pi.LocationID
WHERE pi.ModifiedDate >= @modifiedSince
ORDER BY pi.ProductID, l.LocationID;
GO
