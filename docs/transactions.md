# Transactions

Transactions begin in the active state with a unique, monotonically allocated nonzero `TransactionId` scoped to one database incarnation. An active transaction may commit or roll back exactly once. Either operation moves it to its corresponding terminal state; a failure in terminal processing moves it to `Failed`. Storage entry points call `EnsureActive` before accepting work.

Disposal is deterministic. Disposing active work invokes rollback once, while disposing a committed, rolled-back, or failed transaction has no additional effect. Transaction IDs are not reused within an incarnation; a persisted last-allocated value can seed the manager after reopen.

Before mutating heap, index, overflow, or catalog bytes, the transaction records an in-memory before-image or intent-specific undo action. Undo actions execute in strict reverse mutation order, followed by reverse-order reclamation of pages allocated by the transaction. Retired pages are tracked separately and are not reused during rollback. If any undo or reclamation action fails, the database recovery-required marker is set before the failure escapes.
