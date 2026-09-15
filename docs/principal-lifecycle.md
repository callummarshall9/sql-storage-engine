# Principal lifecycle authority (1.16.0)

Database-scoped `StorageDatabasePermissionAction.ManagePrincipals` is independent of table `ManagePermissions`.
Trusted bootstrap uses `SetDatabasePermissionAsync`; ordinary object permission and admission APIs reject the audit-only
`StoragePermissionAction.ManagePrincipals`. An active actor needs a grant and no deny. `ChangePrincipalAsync` checks the
exact database identity and security revision under the existing exclusive transaction root.

Create requires an unused non-retired issuer/subject UUID pair. Deactivate retains grants for compatibility; repeating it
is an audited no-op. Retire removes the principal, direct/database grants and membership references and permanently
records its identity. Ownership/delegation references block retirement until explicitly removed. Both lifecycle create and
legacy trusted `SetPrincipalAsync` reject retired IDs. An uncommitted creation/retirement rolls back normally; only committed
identities/tombstones constrain later transactions. Duplicate creation/retirement and operation replay fail explicitly.

`SetPrincipalDependencyAsync` is a trusted reference index, not a role or impersonation implementation. Future authoritative
membership/ownership/delegation writers must maintain this index and their records atomically. No such richer records or
SQL commands are currently admitted. Removing membership here removes the complete currently supported membership reference.
No principal or role inference, ownership privilege, password handling or authentication is introduced.

Lifecycle success audit records actor, target, operation/correlation IDs, database ID, operation and revision. State and audit
commit or roll back together, including savepoints. Denial callers must abort their user root and use the existing independent
Denied event path; the operation itself never grants authority on audit failure. Catalog/audit failure poisons the callback
and rolls back its effects. Cancellation aborts the root; disposal rolls back an unresolved root. Root receipts remain the
only durable outcome oracle and ambiguous commits require resolution rather than guessed replay.

Catalog format 12 prevents older readers ignoring retirement metadata. Formats 6–11 remain readable, with missing lifecycle
fields initialized empty. New writes upgrade the format; back up before migration. Quotas are 1,024 live principals,
1,024 permanent tombstones, 1,024 database grants, 1,024 dependency entries plus existing 1,024 object grants, 4,096 mutation
audits and 4 MiB security payload. There is no tombstone eviction or UUID recycling. Exact/one-over operations fail atomically.
Whole-file roots, security payload rewrites and existing bounded savepoint/cancellation semantics remain unchanged.

Tests: PrincipalLifecycleTests, PrincipalLifecycleRecoveryTests, PrincipalLifecycleQuotaTests, PrincipalLifecycleCodecTests,
and lifecycle phases in ExplicitTransactionCrashTests. These cover persisted authority removal, escalation denial, duplicate,
stale/foreign requests, dependency guards, cancellation, root/savepoint/disposal, corrupted state, legacy migration and actual
process-exit recovery. No new SQL capability is claimed by this provider release.

Validation on 2026-09-15: full Debug and Release each pass 526 tests with warnings treated as errors. Focused fault,
quota, migration and abrupt-exit cases pass. Local Release observations (no timing threshold): one retirement callback
0.512 ms; a commit retaining exactly 1,024 tombstones 0.228 ms, database file 483,328 bytes. This is a bounded local
administration baseline, not a throughput claim. Existing constructor/deconstruction shape of StorageAuditRecord is
preserved; new target/operation metadata is exposed through additive init properties.
