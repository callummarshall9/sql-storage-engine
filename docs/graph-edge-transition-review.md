# Combined graph edge transition — 2026-09-11

Version 1.11.0 adds UpdateAndReconnectAsync to IStorageGraphEdgeTable. Payload and
validated endpoints enter one TableStorage graph update: identity is retained,
rowversion and computed payloads evaluate once, and constraints see only the final
replacement. Existing ReconnectAsync delegates with an empty payload update.
Statement-scoped handles retain the existing journal, lifetime, and cancellation
contracts. No catalog format, SQL syntax, or transaction lifetime changes.

Seven GraphEdgeTransitionTests cases cover direct/scoped success, a constraint that
rejects either intermediate call order, exact single rowversion advancement,
computed payload, commit/reopen, cancellation/check/resource rollback and retry,
payload and adjacency index restoration, missing/foreign endpoints, missing edge,
generated-column rejection, expired scopes, and active-journal recovery.
Recovery reconstructs an interrupted journal; it does not claim hardware power-loss
qualification. Existing 401 tests remain enabled.

This is the storage correction requested during execution GRAPH-001.04. It does not
complete that execution story or define a SQL endpoint-update extension.
Third-party IStorageGraphEdgeTable implementations must implement the new method.
Validation: 408 tests passed in each of Release and Debug with warnings as errors.
Focused formatting, package creation, and whitespace checks passed before publication.
