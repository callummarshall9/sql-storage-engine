# Statement-scoped graph API review — 2026-09-11

## Scope and outcome

Supplies the public storage prerequisite for execution-engine GRAPH-001.04 (#244).
Version 1.10.0 adds node/edge open methods to IStorageStatement. Existing graph
interfaces and validation are reused; scoped mutations defer publication to the
existing durable statement journal. Root-handle behavior is retained, with a
post-lock generation check to reject handles invalidated by a waiting rollback.
IStorageStatement and StorageStatement now each have their own source file.

No SQL executor DML, OUTPUT, graph DDL, multi-statement transactions, package-pin
updates, or new persistence format is included. Existing callers can continue using
root handles; third-party IStorageStatement implementations must implement the two
new methods when recompiling.

## Roadmap cross-cutting gate

- Correctness: eight new GraphStatementTests cases plus six existing GraphStorageTests
  cases pass. Mixed node/edge and ordinary-table mutations verify commit and rollback,
  payload indexes, graph identity, both adjacency directions, and retry.
- Persistence: committed state survives reopen. Active-journal recovery restores graph
  identities, payloads, endpoints, and adjacency after persisted mutations.
  Existing format/backward-compatibility tests remain enabled.
- Failure behavior: referential rejection, cancellation, actual traversal resource
  exhaustion, stale waiting root handles, expired success/failure scopes, engine
  disposal, and early iterator disposal are covered. Existing I/O, allocation,
  corruption, and recovery tests run unchanged in the full suite.
- Documentation: quickstart usage and transaction ownership, failure propagation,
  retry, compatibility, and unsupported behavior are recorded.
- Code quality: Release and Debug full-suite builds use warnings as errors; focused
  formatting verification and whitespace validation pass. New types have separate
  files; no page/index implementation details enter the public API.

## Evidence and limitations

Focused graph tests: 14 passed. Full Release and Debug suites: 401 passed each,
zero skipped. Release package build produces SqlStorageEngine 1.10.0 and symbols.
No tests or acceptance criteria were weakened.

Recovery evidence reconstructs an interrupted active journal; it is not a new
process-kill matrix or hardware power-loss qualification. The database-wide journal
still copies the database once per statement. Callbacks must await operations
sequentially, let failures escape to request rollback, and not use root handles
while holding the statement gate. Ambiguous post-commit retries need caller-owned
idempotency. Graph catalog registration remains outside this statement API.

Release/push and execution package adoption remain separate completion steps.
Execution #244 remains open: supplying this API does not complete SQL graph DML.
