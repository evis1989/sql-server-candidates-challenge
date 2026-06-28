## Context

`add-get-orders-handler` deliberately did not batch the `IN` list, documenting the 2100-parameter cap as an accepted limitation "irrelevant at the documented sample volumes". That assumption broke once a wide `modifiedSince` window (an initial/full sync, or test data whose `ModifiedDate` was bumped en masse) returned tens of thousands of orders, producing the SQL Server error "the incoming request has too many parameters (max 2100)". The limitation became a real correctness bug, so it is now fixed.

## Goals / Non-Goals

**Goals:** make `GetOrders` succeed for any header-window size; keep the change minimal and the batching path unit-testable; no change to the SQL shape, models, or contract.

**Non-Goals:** no table-valued parameters or temp tables (a simple chunk loop is enough); no streaming/paging of the result back to the platform; no change to the other handlers.

## Decisions

- **Chunk the ids, run the detail query per chunk.** `AttachDetails` iterates the orders in batches of `DetailBatchSize`, calling the existing `BuildDetailSql(batch.Count)` / `BindIds(batch)` per chunk and stitching each batch's rows into the orders by id. Reusing the existing per-query helpers means the only new logic is the loop. Alternative — a table-valued parameter — was rejected as heavier (a TVP type + DataTable marshalling) for no benefit at these sizes.
- **Batch size 1000.** Comfortably below the 2100 cap with headroom, and large enough that even tens of thousands of orders take only a few dozen detail round-trips. One constant, easy to tune.
- **Injectable batch size (test seam).** A second constructor takes the batch size so a test can drive batching with a size of 2 and three orders, instead of needing 1000+ rows. The public constructor keeps the 1000 default, so production behavior is fixed. Mirrors the retry publisher's backoff seam.
- **No behavior change for small sets.** With fewer orders than the batch size the loop runs once — identical to the previous single-query path, so the existing stitch/empty/parameter tests stay green unchanged.

## Risks / Trade-offs

- **Many small detail queries instead of one** for very large windows (e.g. ~32 queries for ~32k orders). Acceptable: each is an indexed lookup, and large windows are rare (incremental syncs are small; only a first full sync is big). If it ever became a hotspot, a TVP or a temp-table join would replace the loop.
- **Batches are independent commands**, so there is no single transactional snapshot across them. The data is read-only sync extraction, so a row changing between batches is not a correctness concern here.
