# SQL Storage Engine Product Roadmap

## 1. Product vision

Build a small, understandable, durable SQL storage engine that can:

- Persist typed relational data.
- Retrieve rows through table scans and B+ tree indexes.
- Perform atomic inserts, updates, and deletes.
- Recover safely after a process or machine failure.
- Provide stable APIs for a SQL planner and execution engine.
- Remain approachable enough for contributors to understand the complete system.

The product should evolve from a tested in-memory B+ tree into a durable, transactional storage engine without prematurely adopting the complexity of a mature distributed database.

The accompanying [storage-plan.md](storage-plan.md) describes the proposed technical architecture. This roadmap defines the order in which the product should gain those capabilities and the evidence required before each release is considered complete.

## 2. Target users

### Primary users

- Developers building a SQL parser, planner, or executor.
- Contributors learning how relational storage engines work.
- Applications requiring a small embedded relational store.

### Secondary users

- Educators demonstrating database internals.
- Engineers experimenting with page layouts, indexes, and recovery.
- Test tooling that needs deterministic storage-engine behavior.

## 3. Product principles

### Correctness before performance

A slower operation that preserves data is preferable to a fast operation with unclear failure behavior. Every optimization must preserve documented invariants.

### Explicit durability guarantees

The product must clearly state whether an operation is in memory, buffered, committed, or durable. `Commit` must have a precise meaning.

### Stable logical APIs

The SQL layer should depend on tables, rows, indexes, and transactions. It should not depend on page layouts, B+ tree nodes, buffer frames, or log-record formats.

### Incremental complexity

Each release should be useful and testable on its own. MVCC, fine-grained locking, compression, and advanced query optimizations should not block the first durable engine.

### Observable and diagnosable

Storage structures should provide validation, metrics, and actionable errors. Corruption must be reported rather than silently ignored.

### Understandable implementation

Names, comments, tests, and documentation should explain intent and invariants. Contributors should be able to trace a row from a SQL operation to its bytes on disk.

## 4. Product evolution

```mermaid
flowchart LR
    M0[M0: In-memory index]
    M1[M1: Page foundation]
    M2[M2: Durable heap]
    M3[M3: Persistent indexes]
    M4[M4: Catalog and tables]
    M5[M5: Transactions]
    M6[M6: Crash recovery]
    M7[M7: SQL-ready storage]
    M8[M8: Multi-writer concurrency]
    M9[M9: Backup and lifecycle]
    M10[M10: Production qualification]

    M0 --> M1
    M1 --> M2
    M2 --> M3
    M3 --> M4
    M4 --> M5
    M5 --> M6
    M6 --> M7
    M7 --> M8
    M8 --> M9
    M9 --> M10
```

| Milestone | Product outcome | Durability level |
|---|---|---|
| M0 | Generic in-memory B+ tree | None |
| M1 | Reopenable page file | Raw pages |
| M2 | Reopenable heap tables | Rows |
| M3 | Reopenable B+ tree indexes | Index entries |
| M4 | Named tables and indexes | Schema and catalog |
| M5 | Atomic logical operations | Controlled shutdown |
| M6 | Crash-safe commits | Process and machine failure |
| M7 | Stable SQL storage API | SQL workloads |
| M8 | Multi-writer transactional engine | Defined isolation |
| M9 | Backup, PITR, upgrades, and maintenance | Operational durability |
| M10 | Qualified production release | Supported production contract |

## 5. Current baseline — M0

### Available capability

- Generic `IBPlusTree<TKey, TValue>`.
- Duplicate-key support.
- Exact and range lookup.
- Ascending and descending scans.
- Pair-specific deletion.
- Separate internal and leaf node types.
- Leaf sibling links.
- Randomized insertion and deletion tests.

### Current limitation

The B+ tree is an in-memory data structure. Its nodes are C# objects and disappear when the process ends. There are no table pages, rows, catalog, transaction log, or recovery mechanism.

### Exit criteria

- Public B+ tree behavior is documented.
- Tree invariants are tested across supported orders.
- The existing test suite is green.
- The in-memory implementation is retained as a reference model for testing the future page-backed implementation.

## 6. M1 — Page foundation

### Product outcome

The engine can create a database file, allocate fixed-size pages, write them, close the file, reopen it, and retrieve the same bytes.

This is the foundation for every durable feature.

### User stories

- As an engine developer, I can create and reopen a database file.
- As a storage component, I can request a page without managing file offsets.
- As an operator, I receive a clear error when a file has an unsupported format or invalid header.
- As a test author, I can use an in-memory page store with the same interface as the file-backed implementation.

### Epics

#### Strong identifiers

- Add `PageId`, `DatabaseId`, and format-version types.
- Avoid accepting raw numeric identifiers across component boundaries.

#### Database header

- Define magic number, page size, format version, and allocation metadata.
- Validate headers on open.
- Reject incompatible versions without modifying the file.

#### Page store

- Implement fixed-size reads and writes.
- Allocate new pages.
- Free and reuse pages.
- Extend the file when no reusable page exists.

#### Common page header

- Store page ID, type, version, checksum, and page log sequence number.
- Validate page identity and type before decoding.

#### Test page store

- Provide deterministic in-memory storage.
- Support injected read and write failures.

### Acceptance criteria

- Creating a database produces a valid page-zero header.
- Closing and reopening preserves all written pages.
- Freed pages can be reused without overlapping live pages.
- Short reads, invalid checksums, wrong page types, and unsupported versions fail explicitly.
- The file is never partially initialized and then reported as valid.
- Tests cover boundary page IDs and file-extension behavior.

### Out of scope

- Rows.
- Index persistence.
- Transactions.
- Write-ahead logging.

## 7. M2 — Buffer pool and durable heap

### Product outcome

The engine can store, retrieve, update, delete, and scan encoded rows in heap-organized tables. Rows survive a clean restart.

### User stories

- As a table implementation, I can insert encoded row bytes and receive a stable `RowId`.
- As an executor, I can retrieve a row by `RowId`.
- As an executor, I can scan every live row.
- As a storage engine, I can cache pages and safely flush dirty data.
- As an application, I can store values larger than one ordinary row.

### Epics

#### Buffer pool

- Pin and unpin pages.
- Prevent eviction of pinned pages.
- Track dirty pages.
- Add clock or LRU replacement.
- Flush individual pages and the complete pool.

#### Slotted heap pages

- Add slot-directory layout.
- Support variable-sized records.
- Reuse deleted slots.
- Compact fragmented page space.
- Track slot generations.

#### Row identifiers

- Define `RowId(PageId, SlotId, Generation)`.
- Reject deleted, reused, or out-of-range slots.

#### Table heap

- Link or otherwise enumerate table heap pages.
- Insert into a page with sufficient free space.
- Read, update, delete, and scan.

#### Free-space map

- Locate candidate heap pages efficiently.
- Update estimates after every mutation.
- Reconstruct the initial implementation when opening the database.

#### Row codec

- Encode fixed-width and variable-width SQL values.
- Encode nullability.
- Decode complete rows.
- Locate and update selected columns.
- Reject malformed rows.

#### Overflow manager

- Keep small values inline.
- Store large values in overflow chains.
- Read and free chains.
- Replace chains using copy-on-write.
- Detect truncated and cyclic chains.

### Acceptance criteria

- Rows survive close and reopen.
- A stale `RowId` cannot resolve to a replacement row.
- Page compaction preserves every live row and `RowId`.
- Variable-length updates either remain in place or return a new `RowId`.
- Large values survive restart and are reclaimed after deletion.
- Pinned pages cannot be evicted.
- Dirty pages are flushed without corrupting clean pages.
- A randomized heap test agrees with an in-memory reference model.

### Product constraint

At this milestone durability is guaranteed only after an explicit flush and clean shutdown. Crash recovery is not yet promised.

## 8. M3 — Persistent B+ tree indexes

### Product outcome

The engine can create a page-backed B+ tree mapping `IndexKey` values to `RowId` values. Indexes survive restart and support equality and range access.

### User stories

- As an executor, I can find matching row IDs through an index.
- As an executor, I can scan an index range in either direction.
- As a table implementation, I can maintain duplicate and unique indexes.
- As an engine developer, I can validate a persistent index after reopening it.

### Epics

#### Page-backed nodes

- Serialize internal separators and child page IDs.
- Serialize leaf key/value entries.
- Persist previous and next leaf page IDs.
- Replace object references with pinned pages.

#### Key codec

- Encode primitive keys.
- Encode composite keys.
- Define null ordering.
- Support ascending and descending index fields.
- Add deterministic text collation behavior.

#### Structural operations

- Insert and split leaf pages.
- Split internal pages.
- Borrow during deletion.
- Merge underfilled pages.
- Grow and contract the root.
- Persist root page changes.

#### Index API

- Exact lookup.
- Duplicate-key lookup.
- Lower and upper bounds.
- Inclusive and exclusive range scans.
- Pair-specific deletion.
- Unique-key validation.

#### Reference-model testing

- Execute identical operations against the existing in-memory tree and the page-backed tree.
- Compare ordered output after every operation.

### Acceptance criteria

- Index contents survive reopen after every structural operation.
- All leaves remain at the same depth.
- Occupancy and separator invariants hold.
- Leaf links remain bidirectionally consistent.
- Duplicate keys spanning multiple pages are found and removed correctly.
- Unique indexes reject logical duplicates.
- Root page changes are persisted.
- Randomized tests cover orders, duplicates, and mixed operations.

### Performance target

Lookup, insertion, and deletion should touch a number of index pages proportional to tree height rather than total entry count.

## 9. M4 — Catalog, tables, and indexes

### Product outcome

Users can create named tables and indexes, close the database, reopen it, and continue using them through stable logical APIs.

### User stories

- As an executor, I can create and open a table by name.
- As an executor, I can create an index over one or more columns.
- As a planner, I can inspect table and index metadata.
- As a user, all indexes are maintained automatically when rows change.

### Epics

#### Catalog bootstrap

- Define a fixed system catalog format.
- Persist table, column, and index definitions.
- Persist heap entry pages and index root pages.
- Cache decoded metadata.

#### Table API

- Insert logical rows.
- Read and scan rows.
- Apply partial row updates.
- Delete rows.
- Hide heap and index coordination from callers.

#### Index lifecycle

- Create an empty index.
- Build an index from an existing table scan.
- Drop an index and reclaim its pages.
- Open all indexes when opening a table.

#### Constraint enforcement

- Nullability.
- Unique indexes.
- Primary keys represented as unique indexes.
- Type and column-count validation.

### Acceptance criteria

- Tables and indexes remain discoverable after restart.
- Inserting a row updates every applicable index.
- Updating an indexed key removes the old index entry and adds the new one.
- Moving a row updates its `RowId` in every index.
- Deleting a row removes all associated index entries and overflow data.
- Failed validation does not modify heap or index state.
- Building a secondary index produces the same entries as a complete table scan.

### Key product boundary

The SQL executor submits one table operation. It does not separately mutate heap storage and indexes.

## 10. M5 — Atomic transactions

### Product outcome

Logical operations spanning heap pages, indexes, overflow pages, and catalog records either complete fully or roll back fully during normal process execution.

### User stories

- As an application, I can commit a set of related changes.
- As an application, I can roll back an active transaction.
- As an engine, I can undo a partially completed insert, update, delete, or index build.
- As a caller, disposing an uncommitted transaction safely rolls it back.

### Epics

#### Transaction lifecycle

- Begin, commit, rollback, and dispose.
- Transaction IDs and states.
- Invalid-state protection.

#### Undo tracking

- Record page changes required for rollback.
- Track allocations, deferred frees, and overflow replacements.
- Undo operations in reverse order.

#### Single-writer coordination

- Permit multiple readers.
- Permit one active writer.
- Define reader visibility.

#### Atomic table operations

- Coordinate heap and all index changes.
- Ensure unique checks and writes share one transaction.
- Delay page reuse until rollback is impossible.

### Acceptance criteria

- Rolling back an insert leaves no row, index entry, or overflow chain.
- Rolling back an update restores old row bytes and index keys.
- Rolling back a delete restores the row and its indexes.
- A failed unique-index update leaves the previous state intact.
- Transaction disposal rolls back active work.
- Readers do not observe half-completed table operations.

### Product constraint

Transactions are atomic during a running process, but committed changes are not yet guaranteed to survive an uncontrolled crash until M6.

## 11. M6 — Write-ahead logging and crash recovery

### Product outcome

Once `Commit` returns successfully, committed changes survive process termination and restart. Incomplete transactions are removed during recovery.

### User stories

- As an application, I can rely on committed data surviving a crash.
- As an operator, reopening a database automatically performs recovery.
- As an engineer, I can reproduce failures using deterministic crash injection.
- As the engine, I never write a changed database page before its corresponding log record is durable.

### Epics

#### WAL format

- Log header, checksums, sequence numbers, and transaction IDs.
- Begin, change, commit, rollback, and checkpoint records.
- Detection of torn or incomplete trailing records.

#### WAL integration

- Assign a log sequence number to every page mutation.
- Enforce write-ahead ordering in the buffer pool.
- Flush commit records before reporting success.

#### Recovery manager

- Analysis of committed and incomplete transactions.
- Redo committed or potentially missing page changes.
- Undo incomplete transactions.
- Idempotent repeated recovery.

#### Checkpointing

- Record recovery starting points.
- Track dirty pages and active transactions.
- Bound normal recovery time.

#### Crash-injection harness

- Terminate after individual WAL appends, WAL flushes, page mutations, and page writes.
- Reopen and compare state with the expected transaction boundary.

### Acceptance criteria

- A returned commit survives immediate forced termination.
- An incomplete transaction is absent after recovery.
- Recovery can itself be interrupted and safely restarted.
- Torn WAL tails are detected and ignored or rejected according to policy.
- A B+ tree split interrupted at every write boundary recovers to a valid tree.
- Overflow-chain replacement cannot leave the committed row pointing at incomplete data.
- Root page changes and catalog updates recover atomically.

### Release significance

This is the first release that can accurately describe itself as a durable transactional storage engine.

## 12. M7 — SQL-ready storage API

### Product outcome

The storage engine provides a documented, stable contract suitable for a SQL planner and executor.

### User stories

- As a planner, I can discover tables, columns, constraints, and indexes.
- As an executor, I can choose table scans or index scans.
- As an executor, I can request only required columns.
- As an application, I receive stable error types for expected failures.
- As a contributor, I can diagnose page and index state without reading raw files.

### Epics

#### Stable public interfaces

- `IStorageEngine`.
- `IDatabase`.
- `ICatalog`.
- `ITable`.
- `IIndex`.
- `ITransaction`.

#### Scan capabilities

- Full table scans.
- Exact index lookup.
- Bounded index scans.
- Ascending and descending order.
- Projection of required columns.
- Scan cancellation and deterministic resource disposal.

#### Error model

- Table and index not found.
- Duplicate key.
- Transaction conflict.
- Unsupported format.
- Storage corruption.
- Capacity and resource exhaustion.

#### Diagnostics

- Database information command.
- Table and index statistics.
- Page counts and free-space summaries.
- Integrity checker.
- Human-readable structure dumps for tests and debugging.

#### Documentation

- API reference.
- File-format overview.
- Transaction guarantee.
- Recovery behavior.
- Examples covering table and index usage.

### Acceptance criteria

- A SQL executor can implement insert, select, update, and delete without accessing page APIs.
- All iterators release pinned pages when completed early.
- Public behavior is documented and covered by contract tests.
- Corruption produces a specific error and does not trigger silent writes.
- Integrity checks cover heap pages, indexes, catalog references, overflow chains, and free pages.

## 13. M8 — Multi-writer concurrency

### Product outcome

The engine supports multiple concurrent writers with documented isolation, bounded blocking, deadlock handling, and cancellation-safe resource ownership.

### Epics

#### Strict two-phase locking

- Row or key-level transaction locks.
- Page latches.
- Write locks retained through commit or rollback.
- Lock escalation rules.
- Deadlock detection and deterministic victim selection.
- Lock timeouts and cancellation.

#### Isolation levels

- Precisely document read committed behavior.
- Add repeatable read.
- Add key-range locking for serializable scans.
- Test dirty reads, non-repeatable reads, lost updates, and phantoms.

#### Concurrent tree and heap operations

- Safe page splits while readers traverse indexes.
- Safe scans while leaves split or merge.
- Safe heap compaction while rows are visible.
- Latch ordering rules.
- No transaction-lock waits while holding page latches.

#### Resource governance

- Transaction duration and undo limits.
- Per-operation pin budgets.
- Concurrent scan and transaction limits.
- Cancellation tokens for blocking operations.
- Bounded queues for lock and I/O waiters.

#### MVCC evaluation

- Row versions.
- Visibility rules.
- Snapshot isolation.
- Vacuuming obsolete versions.

MVCC remains a product decision rather than an M8 requirement. It should be introduced only if measured workloads justify its storage, vacuum, and recovery complexity.

### Acceptance criteria

- Histories match the documented isolation level.
- No lost updates occur.
- Deadlocks resolve without leaking locks, pins, or transactions.
- Cancellation removes waiters safely.
- Long-running randomized concurrent workloads preserve heap/index consistency.
- Concurrent scans neither omit nor duplicate committed rows beyond documented isolation semantics.
- Lock, latch, and transaction waits are observable.

## 14. M9 — Backup, recovery lifecycle, upgrades, and maintenance

### Product outcome

Operators can protect, restore, upgrade, inspect, and maintain databases without relying on ad hoc file copies.

### User stories

- As an operator, I can create a verified backup while the database remains available.
- As an operator, I can restore to a selected point in time.
- As an operator, I can upgrade a database through a documented, restartable procedure.
- As an operator, I can identify fragmentation, corruption, and unreclaimed space.
- As an application, maintenance work does not monopolize all storage resources.

### Epics

#### Backup and point-in-time recovery

- Offline physical backups.
- Online backups anchored to an LSN.
- Backup manifests and checksums.
- WAL archival and retention.
- Point-in-time replay to an LSN or timestamp.
- Database timeline/incarnation tracking after recovery.
- Automated restore verification.

#### Format lifecycle

- Golden files for every supported format.
- Restartable upgrade steps.
- Explicit downgrade and rollback policy.
- Database identity and compatibility validation.
- Pre-upgrade backup enforcement.

#### Maintenance

- Heap-page compaction.
- Deferred page reclamation.
- Overflow orphan cleanup.
- Index rebuild.
- Catalog statistics.
- Vacuum or garbage collection.

#### Integrity and repair

- Online read-only integrity checks where safe.
- Offline exhaustive integrity checks.
- Machine-readable findings.
- Repair only when a deterministic source of truth exists.
- Never silently discard corrupted rows or index entries.

#### WAL lifecycle

- Checkpoints.
- WAL segment archival.
- Safe segment truncation.
- Recovery-distance reporting.
- Retention alarms.

### Acceptance criteria

- Online backups restore to a consistent database.
- Point-in-time recovery stops at the requested target.
- Restore tests run automatically rather than relying on manual confirmation.
- Recovery rejects WAL from another database or timeline.
- Upgrades can be interrupted and restarted safely.
- Every supported engine version opens the golden files it claims to support.
- Maintenance is interruptible, restartable, rate limited, and observable.
- WAL cannot be removed while required by recovery, backup, or another registered consumer.

## 15. M10 — Production qualification

### Product outcome

The engine has a published support envelope and objective evidence that its durability, recovery, security, compatibility, and operational claims hold on every supported platform.

### Epics

#### Platform qualification

- Supported operating-system and filesystem matrix.
- Verified flush and atomic-rename behavior.
- Sector and page-size assumptions.
- Local, removable, and network-storage policy.
- Repeated power-loss or equivalent fault testing.

#### Torn-write protection

- Full-page WAL images or a double-write mechanism.
- Per-page and per-WAL-record checksums.
- Recovery tests for partial database and log writes.
- No automatic overwrite of unexplained checksum failures.

#### Fault-injection program

- Short reads and writes.
- Interrupted system calls.
- Disk-full and quota failures.
- Flush failures.
- Allocation and out-of-memory failures.
- Process termination at every durable write boundary.

#### Security hardening

- Fuzz every persistent decoder.
- Checked size and offset arithmetic.
- Allocation limits for file-controlled values.
- Least-privilege file creation.
- Sensitive-value redaction.
- Threat model and security review.

#### Observability and supportability

- Structured metrics and stable error categories.
- Database, page, WAL, backup, and transaction diagnostics.
- Capacity and limit reporting.
- Support bundle generation without exposing user values by default.

#### Capacity and performance qualification

- Read-ahead for scans.
- More efficient free-space maps.
- Prefix compression for index keys.
- Covering indexes.
- Batched writes.
- Group commit.
- Larger-object extent storage.

### Acceptance criteria

- No known data-loss or silent-corruption defect remains open.
- Crash and torn-write matrices pass on every supported platform.
- Backup, restore, upgrade, rollback, and PITR drills pass automatically.
- Recovery is idempotent when interrupted repeatedly.
- Soak tests complete with periodic crash/restart and integrity checks.
- Resource and capacity limits are documented and enforced.
- Decoder fuzzing and security review have no unresolved critical findings.
- Latency, throughput, recovery-time, and space budgets meet published objectives.
- A release candidate remains stable through a defined qualification window.
- Operational runbooks and incident-response procedures are complete.

## 16. Cross-cutting quality gates

Every milestone must satisfy the following gates.

### Correctness

- Unit tests for local algorithms.
- Property-based or randomized model tests for mutable structures.
- Integration tests across component boundaries.
- Regression tests for every discovered defect.

### Persistence

- Close-and-reopen tests for every durable structure.
- Backward compatibility tests once a file format is released.
- Clear rejection of unknown versions.

### Failure behavior

- Injected I/O errors.
- Allocation failures.
- Corrupt or truncated input.
- Interrupted operations.
- Resource cleanup after exceptions.

### Documentation

- Public API documentation.
- Updated architecture documentation.
- Recorded invariants for each page type.
- Migration notes for breaking changes.

### Code quality

- No build warnings.
- Explicit ownership and disposal.
- No public exposure of mutable page or node internals.
- Intent-focused comments around non-obvious storage invariants.
- Junior-readable tests describing expected behavior.

## 17. Product metrics

Metrics should measure correctness and usability before optimization.

### Reliability

- Number of crash-injection points tested.
- Recovery success rate.
- Integrity-check failure rate after randomized workloads.
- Number of known data-loss or corruption defects.

### Quality

- Test coverage across page types and mutation paths.
- Public API contract coverage.
- Reopen-test coverage.
- Time required to diagnose an intentionally corrupted page.

### Performance

- Point-lookup page reads.
- Range-scan throughput.
- Insert/update/delete throughput.
- Buffer-pool hit rate.
- WAL bytes per transaction.
- Database bytes per logical row.
- Recovery time from the latest checkpoint.

Performance targets should be baselined at M3 and made release gates only after M6 correctness is established.

## 18. Major risks

| Risk | Impact | Mitigation |
|---|---|---|
| Page format changes repeatedly | Rework and incompatible files | Version every page and delay compatibility promise until M6 |
| Index and heap diverge | Incorrect query results | Coordinate changes in table transactions and add cross-checking integrity tests |
| Buffer pool violates WAL ordering | Committed or partially written corruption | Centralize dirty-page flushing and require page LSN checks |
| Row relocation creates stale indexes | Reads target missing rows | Return new `RowId` and update every index atomically |
| Reused slots satisfy stale row IDs | Incorrect rows returned | Include slot generation in `RowId` |
| Overflow pages leak | Unbounded file growth | Track ownership transactionally and add orphan checks |
| Recovery is added too late | Existing formats cannot be logged safely | Introduce page LSN fields in M1 even before WAL exists |
| Premature concurrency work | Delays durable MVP | Keep single-writer design through M7 |
| Generic abstractions obscure binary layout | Hard-to-debug persistence | Separate logical APIs from explicit page codecs |
| Filesystem flush assumptions are wrong | Acknowledged commits are lost | Publish a support matrix and qualify every platform |
| Torn pages survive WAL recovery | Undetected or unrecoverable corruption | Add checksums and full-page images or double-write protection |
| Backups cannot be restored | False sense of safety | Automate restore and integrity verification |
| WAL retention grows without bound | Storage exhaustion | Track recovery consumers and alarm before limits |
| Malformed files trigger unsafe allocations | Denial of service or compromise | Bound all decoded sizes and continuously fuzz decoders |

## 19. Deliberately deferred capabilities

The following are not required for the first durable SQL-ready release:

- Distributed storage.
- Replication.
- Sharding.
- Network protocol.
- Cost-based query optimization.
- Joins and SQL expression evaluation.
- MVCC unless justified by workload evidence.
- Online schema migration.
- Compression.
- Encryption at rest.
- Shared large-object deduplication.
- Full-text or spatial indexes.

These may become future products or extensions. They should not complicate the core storage roadmap without a demonstrated need.

## 20. Release strategy

### Development releases

M1–M5 are development releases. File formats may change and migration is not guaranteed.

### First durable preview

M6 becomes the first durability preview. Its file format should be versioned and treated as a compatibility candidate.

### First SQL-ready release

M7 is the first release intended for integration with a SQL execution engine. Breaking public API changes require migration notes.

### Concurrency preview

M8 is a concurrency preview. Isolation behavior is part of the API contract and must not be inferred from implementation details.

### Operational preview

M9 is the first release intended for operators responsible for backup, restore, upgrades, and long-running storage health.

### Production release

M10 is the first production-qualified release. “Production” applies only to the published platform, filesystem, capacity, and workload support envelope.

## 21. Recommended next backlog

The immediate backlog should begin M1:

1. Define page size and the `PageId` type.
2. Define database and common page headers.
3. Write binary header codecs with round-trip tests.
4. Implement an in-memory `IPageStore`.
5. Implement a file-backed `IPageStore`.
6. Add database create, close, and reopen behavior.
7. Add allocation and free-page reuse.
8. Add checksums and invalid-file tests.
9. Write a file-format note with byte offsets.
10. Add failure injection to the test page store.

The existing in-memory B+ tree should remain stable during M1 and M2. Persistent index work begins only after page and buffer ownership rules are proven.

## 22. Definition of product success

The roadmap succeeds when a SQL executor can:

1. Create or open a database.
2. Create tables and indexes.
3. Insert, retrieve, update, delete, and scan typed rows.
4. Use indexes to resolve keys to heap rows.
5. Commit or roll back multi-structure changes.
6. Restart after a forced crash without losing committed work.
7. Detect structural corruption.
8. Use the engine without depending on its page or B+ tree internals.

The product is not complete merely when rows can be written to disk. It becomes a storage engine when those rows remain correct across indexing, updates, failures, recovery, and continued evolution.
