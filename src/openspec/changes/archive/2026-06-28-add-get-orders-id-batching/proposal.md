## Why

`GetOrders` built its detail query as a single `IN (@id0..@idN)` with one parameter per order id. SQL Server caps a command at 2100 parameters, so a `modifiedSince` window returning more than ~2100 orders failed with "too many parameters". This was a documented limitation; it turned out to be reachable (a wide window — e.g. an initial full sync — returns tens of thousands of orders), so this change fixes it by batching the id lookups. Recorded retroactively to keep the spec-driven record complete.

## What Changes

- `GetOrdersHandler` now queries order details in batches: the header ids are chunked (1000 per batch, well under the 2100 cap) and the detail query runs once per chunk, with results stitched into the same orders. Behavior is unchanged for small result sets (a single batch).
- The detail batch size is injectable (a second constructor) so the batching path is unit-testable without thousands of rows.
- Add a test asserting a 3-order result with batch size 2 runs two detail queries binding 2 and 1 ids respectively, and stitches all details correctly.

## Capabilities

### Modified Capabilities
- `get-orders-sync`: the detail lookup is now performed in batches bounded below the SQL Server parameter cap, so arbitrarily large header windows succeed.

## Impact

- Code: `GetOrdersHandler` (batched `AttachDetails`, new test-seam constructor) and one new test. No model, SQL text, or contract change; the per-batch SQL is the same parameterized `IN` as before.
- Removes the previously documented 2100-parameter limitation for `GetOrders`.
- Already implemented and committed; this change is the spec record. Tasks are marked complete.
