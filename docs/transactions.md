# Transactions

Transactions begin in the active state with a unique, monotonically allocated nonzero `TransactionId` scoped to one database incarnation. An active transaction may commit or roll back exactly once. Either operation moves it to its corresponding terminal state; a failure in terminal processing moves it to `Failed`. Storage entry points call `EnsureActive` before accepting work.

Disposal is deterministic. Disposing active work invokes rollback once, while disposing a committed, rolled-back, or failed transaction has no additional effect. Transaction IDs are not reused within an incarnation; a persisted last-allocated value can seed the manager after reopen.
