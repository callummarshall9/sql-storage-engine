# Transactions

Transactions begin in the active state with a unique, monotonically allocated nonzero `TransactionId` scoped to one database incarnation. An active transaction may commit or roll back exactly once. Either operation moves it to its corresponding terminal state; a failure in terminal processing moves it to `Failed`. Storage entry points call `EnsureActive` before accepting work.

Disposal is deterministic. Disposing active work invokes rollback once, while disposing a committed, rolled-back, or failed transaction has no additional effect. Transaction IDs are not reused within an incarnation; a persisted last-allocated value can seed the manager after reopen.

Before mutating heap, index, overflow, or catalog bytes, the transaction records an in-memory before-image or intent-specific undo action. Undo actions execute in strict reverse mutation order, followed by reverse-order reclamation of pages allocated by the transaction. Retired pages are tracked separately and are not reused during rollback. If any undo or reclamation action fails, the database recovery-required marker is set before the failure escapes.

The initial isolation model is read committed with a database-level many-reader/single-writer coordinator. Readers may overlap one another. A writer holds the exclusive resource and a fairness turnstile until commit, rollback, failure, or disposal, so readers cannot observe its half-completed state. Waiting acquisition accepts cancellation and removes itself without retaining either semaphore.

Durable commit appends physical change records with per-transaction previous-LSN links, appends the commit record, flushes through its LSN, and only then transitions to committed and reports success. Append or flush failure produces `Failed` and prohibits later mutation. If communication fails after the flush but before the success response reaches the caller, the transaction remains committed; callers must use higher-level idempotency when retrying that ambiguous outcome.
