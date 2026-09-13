# Explicit storage transactions (1.13.0)

`StorageEngine.BeginTransactionAsync` returns an owned `IStorageTransaction`. Its lifetime spans multiple awaited
`ExecuteStatementAsync` calls; no callback is retained to represent the root. The callback receives an
`IStorageTransactionContext` with catalog, table, index, and graph access. Reads and mutations use that same context.

## Supported profile

The initial isolation profile is `Serializable`: one exclusive database access lease covers the entire root.
Other asynchronous operations wait; synchronous catalog/index acquisition fails with a busy exception so callers
can retry. A second high-level engine cannot open/recover the same database while its owner is alive. Do not access
an owned database using the separately exposed low-level page/file APIs, filesystem aliases, backup tooling, or
external writers. Those APIs are not participants in a high-level transaction.

Explicit transactions currently require Linux. Durable file flushes, atomic rename, and parent-directory `fsync`
are required; the filesystem/device exclusions in `production-support.md` still apply. Process-exit and injected-I/O
checks are evidence for the protocol, not new hardware power-loss qualification.

Supply a positive timeout of at most one day. The deadline starts after begin has prepared its recovery evidence.
Callbacks must observe their cancellation token and await their operations. The owner cancels and rolls back on
expiry, cancellation, or disposal; it does not release isolation while a running callback can still mutate storage.
Parallel operations on a single handle are rejected. Callback-owned streams are disposed when the callback returns,
and escaped handles/streams reject subsequent access. After cancellation, callbacks have a 250 ms grace period to
stop before caller waiting ends with cancellation and an `Indeterminate` state. Storage retains its file ownership
and quarantines access while the callback finishes; disposal reports that cleanup is pending and must be retried.
The root is rolled back once the callback stops. Root undo uses a separate 30-second cooperative cleanup token.
A non-cooperative callback can therefore delay cleanup, but cannot admit another writer or silently commit.

The default and maximum journal quota is 256 MiB of database bytes. Begin rejects a larger database; allocations
cannot grow an active root beyond its configured quota. Both the root and each callback use a whole-database
before-image (plus 64-byte journal header). Budget disk space for two images, recovery/atomic replacement space,
and retained receipts. This deliberately favors correctness over throughput; it is unsuitable for large databases
or high write concurrency. Receipt retention is explicitly owned by the caller.

## Resolution and recovery

Begin writes an outer `.transaction-undo` image and a `.transaction-active` identity before exposing the handle.
Each callback retains existing statement rollback semantics: successful local rollback leaves earlier callbacks
committable; failed local cleanup dooms the root. Root rollback restores heap, catalog, indexes, allocator/header,
and file length and invalidates old handles.

Commit flushes data before writing and flushing its commit decision. Once decision publication begins, an I/O
failure returns `Indeterminate`, never a guessed abort. Storage quarantines ordinary access until reopening.
Recovery consults the durable decision and publishes a terminal receipt keyed by database incarnation and GUID.
An active outer image takes precedence over any nested statement image; recovery cannot restore an intermediate
transaction state. Invalid checksums, contradictory receipts, and identity mismatches fail closed.

`CommitAsync` and `RollbackAsync` return a typed receipt; callers must inspect its state. Retrying the same terminal
operation returns the same receipt; incompatible terminal operations reject. `ResolveTransactionAsync` does not
replay work and returns `Indeterminate` when no terminal evidence exists. After confirmed application handling,
`ReleaseTransactionReceiptAsync` deletes that evidence; later resolution is unknown. A failed begin whose cleanup
is uncertain throws `StorageTransactionBeginException` carrying the identity required for recovery.

No savepoints, ambient transactions, distributed roots, alternate isolation levels, SQL transaction admission,
or live recovery of a quarantined owner are included. SQL execution admission belongs to TXN-001.03.

## Evidence

Named `ExplicitTransaction*Tests` cover multi-callback atomicity, reads of own writes, concurrent visibility,
index-key/insert/delete transitions, exact timeout/quota boundaries, cancellation, idle expiry, disposal, escaped
streams/handles, second-owner exclusion, injected commit/rollback failures, retained receipts, and abrupt child
process exits before/inside/after a root transaction. Existing statement, graph, temporal, and storage tests remain
required. `ExplicitTransactionResourceTests` records journal bytes and elapsed time for 1 and 128 callbacks without
an invented timing threshold.
