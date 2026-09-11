# Transactions

Transactions begin in the active state with a unique, monotonically allocated nonzero `TransactionId` scoped to one database incarnation. An active transaction may commit or roll back exactly once. Either operation moves it to its corresponding terminal state; a failure in terminal processing moves it to `Failed`. Storage entry points call `EnsureActive` before accepting work.

Disposal is deterministic. Disposing active work invokes rollback once, while disposing a committed, rolled-back, or failed transaction has no additional effect. Transaction IDs are not reused within an incarnation; a persisted last-allocated value can seed the manager after reopen.

Before mutating heap, index, overflow, or catalog bytes, the transaction records an in-memory before-image or intent-specific undo action. Undo actions execute in strict reverse mutation order, followed by reverse-order reclamation of pages allocated by the transaction. Retired pages are tracked separately and are not reused during rollback. If any undo or reclamation action fails, the database recovery-required marker is set before the failure escapes.

The initial isolation model is read committed with a database-level many-reader/single-writer coordinator. Readers may overlap one another. A writer holds the exclusive resource and a fairness turnstile until commit, rollback, failure, or disposal, so readers cannot observe its half-completed state. Waiting acquisition accepts cancellation and removes itself without retaining either semaphore.

Durable commit appends physical change records with per-transaction previous-LSN links, appends the commit record, flushes through its LSN, and only then transitions to committed and reports success. Append or flush failure produces `Failed` and prohibits later mutation. If communication fails after the flush but before the success response reaches the caller, the transaction remains committed; callers must use higher-level idempotency when retrying that ambiguous outcome.

## Executor-facing atomic statements

`IStorageEngine.ExecuteStatementAsync` is the high-level multi-row/multi-table mutation boundary. It serializes the
statement against ordinary table reads and mutations, flushes the starting state, and durably writes a checksummed
database before-image to a sibling `.statement-undo` journal before invoking the callback. Statement table mutations do
not flush individually. Successful completion flushes all pages once, durably marks the journal committed, and removes
it. Exception or cancellation restores the before-image and reloads catalog state before the error escapes.

Startup examines the journal before opening the database. An active journal restores the complete pre-statement image;
a committed journal is discarded. The journal is deliberately database-wide in this release: it gives a small embedded
engine a clear crash-atomic contract at the cost of copying the database once per mutation statement. A later WAL-backed
implementation can replace the mechanism without changing the public statement API.

### Statement-scoped graph handles (1.10.0)

The callback can call `IStorageStatement.OpenGraphNodeTableAsync` and
`OpenGraphEdgeTableAsync`. These return the existing graph interfaces, with the same generated
identity, payload, endpoint-existence, referenced-node deletion, and index-maintenance rules.
Node/edge insert, payload update, reconnect, and delete join the enclosing statement's journal:
heap, identity, outgoing/incoming adjacency, payload indexes, and ordinary table mutations
commit or restore together. Reads and traversals see earlier writes in the callback.

Use only statement-scoped handles inside the callback; root handles acquire the statement gate
and must not be called from a callback that already holds it. Await operations sequentially and
dispose enumerators before returning. Scoped handles and enumerators reject use after completion.
Rollback invalidates previously opened root handles; reopen them before retrying. Exceptions must
escape the callback to roll back the entire statement; a caught exception is not a rollback request.
Cancellation before the commit boundary restores the before-image. An ambiguous response after
durable commit still requires caller-owned idempotency; this API does not add automatic retries.

This does not add graph catalog registration inside statements, multi-statement transactions,
or executor-level SQL DML/OUTPUT semantics. No database format change is required.

Version 1.11.0 adds `IStorageGraphEdgeTable.UpdateAndReconnectAsync` for a single
payload-and-endpoint transition. It preserves the edge ID, validates live endpoints,
and evaluates rowversion/computed values, checks, and indexes against the complete
replacement once. Do not emulate it using separate UpdateAsync/ reconnect calls:
those remain two row mutations even inside one journal. The existing endpoint-only
ReconnectAsync delegates to the combined operation with an empty payload update.
This storage operation does not authorize endpoint UPDATE syntax in a SQL dialect.

Evidence: `GraphStatementTests` covers successful mixed mutations, referential failure,
cancellation, traversal resource exhaustion, retry, index restoration, reopen recovery from
an active journal, and expired handles/enumerators. The journal recovery test reconstructs
interrupted on-disk state; it is not a power-loss qualification test.
