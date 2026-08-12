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
