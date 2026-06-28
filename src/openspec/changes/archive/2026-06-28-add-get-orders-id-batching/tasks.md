> Recorded retroactively — implemented and committed before this change was written. Tasks are already complete.

## 1. Batch the detail lookup

- [x] 1.1 Add `DefaultDetailBatchSize = 1000` and an injectable `detailBatchSize` (second constructor) to `GetOrdersHandler`
- [x] 1.2 Rewrite `AttachDetails` to iterate orders in chunks of `_detailBatchSize`, running `BuildDetailSql`/`BindIds`/`MapDetail` per chunk and stitching each batch's details by `salesOrderId`

## 2. Test

- [x] 2.1 Add a test: 3 orders with batch size 2 → two detail queries (binding 2 then 1 ids), all details stitched onto the right orders

## 3. Verification

- [x] 3.1 `dotnet build` clean (0 warnings, 0 errors) and `dotnet test` green (30/30; existing GetOrders tests unchanged)
