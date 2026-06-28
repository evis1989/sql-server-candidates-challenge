## ADDED Requirements

### Requirement: Detail lookup is batched under the parameter cap

The detail query SHALL look up its order ids in batches small enough to stay under the database's command parameter limit, so a header window of any size succeeds. Details from every batch SHALL be stitched into their orders, and a small result set SHALL still use a single batch.

#### Scenario: Large id set is split into batches

- **WHEN** the header query returns more orders than the batch size
- **THEN** the detail query runs once per batch, each binding only that batch's ids, and every batch's details are nested under their orders

#### Scenario: Small id set uses a single batch

- **WHEN** the header query returns fewer orders than the batch size
- **THEN** the detail query runs exactly once for all of them
