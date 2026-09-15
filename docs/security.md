# Versioned permission catalog and durable audit — 1.15.0

This provider enabler supplies host-trusted APIs; it does not authenticate SQL users or automatically authorize ordinary
storage calls. Hosts must authenticate opaque issuer/subject GUID pairs and declare every required access. Execution
engine SQL enforcement is a separate delivery item (AUTH-001.03).

Within an explicit Linux Serializable transaction callback, `context.Security` exposes detached snapshots,
`SetPrincipalAsync`, `SetPermissionAsync`, `ResolveObject` and `ExecuteAuthorizedAsync`. The exclusive database root
lease spans admission through commit/abort, so a competing revocation waits; a previously committed revocation makes
old revisions stale. Each mutation requires the current positive revision. Changed principal/permission state advances
it, including revocation/regrant; duplicate state changes keep it. Overflow rejects without effects. Direct grants only:
Select, Insert, Update, Delete, ManagePermissions; matching deny overrides grant and missing/disabled identities deny.
Permission entries do not imply other actions. Inactive principals retain their grants for explicit host administration.
Roles, impersonation, names as identities and SQL GRANT syntax are outside this provider profile.

Catalog format 11 persists nonempty random object UUIDs. Bindings include database UUID, object UUID and schema version.
Legacy formats 6–10 remain readable; provision a principal before resolving bindings so legacy identity assignment is
journaled. Existing clones preserve UUIDs, and new tables receive new UUIDs even if a rolled-back allocation is reused.
Writing the new format requires 1.15.0 or later to reopen it; back up before upgrade. The existing whole-file journal
covers principals, permissions and mutation audit together with heap/index/catalog changes. A savepoint or root rollback
undoes both data and mutation audit. Only the enclosing root's committed durable receipt confirms success. A failed
required audit poisons the callback even if its exception is swallowed. An indeterminate root uses the existing durable
transaction-resolution API and quarantine rules. Mutation operation IDs cannot be repeated; inspect the persisted
snapshot and resolve the original transaction before deciding what happened, rather than blindly rerunning data effects.

Read admission writes an independent durable event before invoking the callback. It records admission, not stream
completion. Denied attempts must first abort the user root and then call `engine.Security.AppendEventAsync` with a
Denied record. Denial is never converted into permission when audit is unavailable. This sidecar path never reacquires
the data gate; read admission works inside an active transaction and survives savepoint/root rollback. Events contain
only opaque operation/correlation/principal/object identities, typed action/kind/reason and revision; no SQL, row values,
credentials or exception details. Callers supply the typed denial reason. Multi-object admissions use a null aggregate
object identity; callers retain their immutable complete access declaration. Public APIs accept at most 256 accesses.

The provider retains at most 1,024 principals, 1,024 permission entries, 4,096 transaction audit records and 4,096 separate
events. A record is limited to 2 KiB and each serialized security payload to 4 MiB. Limits reject one-over without
truncation or automatic eviction. There is no online retention/deletion API in this first profile; reaching quota blocks
further audited work until an independently designed retention/migration operation is available. Snapshots copy bounded
collections. Catalog updates and sidecar appends rewrite their respective payloads; whole-database root/savepoint copying
retains the existing 256 MiB admission bound. This is a correctness profile, not a high-throughput audit log.

Separate events use a database-bound SHA-256 envelope, flushed temporary file, atomic rename and directory fsync.
Append admission has a 30-second cancellation budget; after rename, durability completes independently of caller
cancellation. OS flush calls themselves cannot be interrupted. Any uncertain publication returns only an opaque
operation ID and quarantines further appends until reopen. `ReadEventsAsync` reports observed records, not a new durable
acknowledgement. After reopen, appending the identical record deduplicates and re-establishes file/directory durability;
conflicting reuse rejects. Corruption rejects reads/appends. Pending files before rename are not committed events.
Engine disposal waits for an in-flight sidecar operation before releasing its exclusive file ownership.

Executable evidence: SecurityCatalogTests, SecurityEventTests, SecurityQuotaTests and the security cases in
ExplicitTransactionCrashTests. Existing transaction/savepoint tests remain the independent root durability oracle.

Validation for this release: 497 tests pass in Debug and Release with warnings as errors; Release pack and changed-file
format verification pass. Repository-wide formatting reports pre-existing findings in 16 untouched files. A local Release
observation at the 4,096-event boundary retained 766,001 bytes and appended in 21.787 ms; this is descriptive evidence,
not a timing threshold. Process-exit tests cover both sides of event publication and root commit/abort.
