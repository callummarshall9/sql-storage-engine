# TXN-001.02 storage review

## Implementation

Version 1.13.1 adds an owned high-level root transaction handle, Serializable database lease, callback enlistment,
root undo journal, durable terminal receipts, recovery, and receipt release. Public table/index implementations
were moved into individual files. The existing low-level WAL transaction API is not silently mixed into this path.

## Learnings

Catalog and statistics access needed the same exclusion gate as row mutations. Root handles must wait rather than
implicitly join a callback. Callback streams require owned disposal before journal resolution. Root recovery must
precede nested statement recovery, including file-length and catalog/handle-generation restoration.

## Architecture

Storage owns all mutable transaction state, locking and recovery. The execution consumer receives only typed public
handles and receipts. Directory synchronization makes file publication/deletion ordering explicit on Linux.
Low-level page/file access and filesystem aliases are outside the high-level ownership contract.

## Debt

Whole-file images and a single database lease deliberately limit concurrency and database size. Non-cooperative
callbacks bound caller waiting through quarantine and deferred rollback but can delay safe cleanup; the engine cannot forcibly stop arbitrary managed code. A quarantined engine
requires reopening. New filesystem or OS qualification, WAL-backed high-level enlistment, and savepoints remain
separate work. No production support-matrix promotion is claimed.

## Refactoring

Added one type per new file, separated transaction resolution from callback lifetime, and extracted existing table
and index implementations. Fixed the existing flush facade to retain its access lease until asynchronous flush
finishes. Removed redundant statistics gate acquisition found by the regression suite.

## Roadmap refinement

The supported provider profile refines the proposed TXN-001.01 surface: one callback context enlists both reads and
writes; synchronous metadata access reports busy while another root owns storage; explicit transactions require
Linux and a maximum 256 MiB database image. Timeout cancellation is cooperative. TXN-001.03 must admit only this
profile and inspect receipts; no SQL transaction command is enabled by this package.

## Exact coverage

29 new tests (461 total) cover success, root/local rollback, reads, heap/index changes, isolation, expiry,
callback cancellation, disposal, exact timeout/quota boundaries, streams, file ownership, faults and three actual
process-exit scenarios. Debug and Release pass with warnings as errors. No existing test was weakened.
The crash probe is built by the solution in both configurations. Focused tests and full regression tests pass.

## Performance

A measured local Debug run used a 41,024-byte root journal: one callback completed in 1.83 ms and 128 callbacks in
55.37 ms. These are observations, not an SLA or regression threshold. Each callback adds another whole-database
snapshot; configured maximum bytes bound growth, not callback count or total I/O. Tests retain their measurements
through `ExplicitTransactionResourceTests` output. Package publication and downstream provenance verification are
recorded by the execution-engine delivery review after publication.

Downstream graph DML qualification found a sequential scan/point-read regression in 1.13.0. Patch 1.13.1 reserves
the callback operation slot per MoveNext, retaining stream ownership until callback completion. The interleaved read
regression passes alongside all existing storage tests; consumers should adopt 1.13.1.
