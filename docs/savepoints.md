# Explicit transaction savepoints

SqlStorageEngine 1.14.0 adds `CreateSavepointAsync(name)` and `RollbackToSavepointAsync(token/name)` to
`IStorageTransaction`. Default interface implementations reject unsupported providers; the concrete engine implements
all three methods. A returned `StorageSavepoint` is a transaction-owned opaque token, not a portable recovery receipt.

Names contain 1–32 UTF-16 code units and compare ordinally, case-sensitively. Creating a duplicate name appends a new
position. Name-based rollback selects the newest live matching point; token-based rollback selects that exact position.
Rolling back invalidates every later point (including duplicate names), retains the selected point for repeated rollback,
and makes older shadowed names visible again. Unknown, forged, foreign, invalidated, disposed or terminal-root tokens
reject. Savepoint operations require a committable root and cannot overlap operations or run inside its callback.

Snapshots restore heap rows, indexes, catalog, allocation/header and file length. Previously acquired handles remain
callback-scoped; callers reopen them in the next callback. Successful rollback preserves earlier work and the root's
exclusive Serializable ownership, allowing further statements and eventual commit. Root completion invalidates all points.

There are at most 32 retained positions. Their combined image bytes (including each 64-byte journal header) must fit the
root's `MaximumJournalBytes` budget, at most 256 MiB. Root undo, a transient rollback guard image and buffers are additional
costs. At most one guard exists because operations serialize. Invalidation reclaims later images. Full-image copying
makes cost proportional to database size and number of points; this is not incremental logging or a throughput claim.

Creation cancellation leaves no published token. Cancellation before rollback rewrite leaves data and points unchanged.
Once rewriting starts, an independent 30-second cleanup deadline completes it; caller cancellation after that point does
not turn successful restoration into failure. A restore fault uses a second independent cleanup deadline to restore the
pre-operation image. Successful recovery keeps the root committable and throws a sanitized IOException; failed recovery
dooms the root and requires root rollback. Root deadline expiry still aborts the root under the existing lifetime policy.

Savepoints are not independently committed durable transactions. Abrupt process exit recovers the outer root's durable
commit/abort decision; an uncommitted root aborts. Recovery never applies a savepoint image as a new root decision, and
removes leftover `.savepoint-*` files after resolving the outer root. Images are internal reserved sidecars protected by
the same exclusive database ownership and filesystem durability requirements as explicit transactions. Receipt retention
and indeterminate root handling are unchanged.

SQL SAVE and named ROLLBACK admission belong to execution-engine TXN-001.05, not this provider package.
