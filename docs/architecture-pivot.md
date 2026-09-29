# Portable storage core architecture pivot

Decision date: 2026-09-15. This is a migration plan; the published 1.16.0 API remains the legacy baseline.
The authoritative cross-repository [architecture decision](https://github.com/callummarshall9/tsql-execution-engine/blob/main/docs/architecture-pivot.md),
[product roadmap](https://github.com/callummarshall9/tsql-execution-engine/blob/main/docs/product-roadmap.md)
and [delivery manifest](https://github.com/callummarshall9/tsql-execution-engine/blob/main/docs/planning/gap-roadmap.json)
own the implementation sequence. Existing closed issues retain their delivery evidence.

## Small mandatory core

Expose provider-neutral opaque key/value collections, consistent snapshots and atomic conditional batches, with explicit
conflict, durability/outcome resolution, cancellation, quotas and disposal contracts. The design spike freezes the exact
API and package names. Pages, B-trees, relational rows, SQL identifiers, principals, permissions, graph constraints and
system databases are not mandatory storage abstractions. Physical indexing and other accelerators are optional narrow
capabilities. Reject unsupported guarantees honestly; do not emulate stronger isolation invisibly.

SQL values and row encoding, catalog schemas, authorization policy, dependency checks, system database lifecycle and SQL
transaction orchestration move into engine/domain services. Backend adapters retain actual storage atomicity, concurrency,
journaling and recovery. This is not merely renaming the current broad interface or moving it to a contracts package.

## Migration and proof

1. [PLUG-001.01 #345](https://github.com/callummarshall9/tsql-execution-engine/issues/345): inventory every public type and concrete import; freeze contracts, profile and ownership.
2. [PLUG-001.02 #346](https://github.com/callummarshall9/tsql-execution-engine/issues/346): publish minimal neutral contracts.
3. [PLUG-001.03 #347](https://github.com/callummarshall9/tsql-execution-engine/issues/347): shared backend conformance harness.
4. [PLUG-001.04 #348](https://github.com/callummarshall9/tsql-execution-engine/issues/348): legacy adapter preserves current guarantees and supported SQL envelope.
5. [PLUG-001.10 #354](https://github.com/callummarshall9/tsql-execution-engine/issues/354): independent persistent append-only backend, not an in-memory fake or wrapper around the old engine.
6. [PLUG-001.11 #355](https://github.com/callummarshall9/tsql-execution-engine/issues/355): shared portable SQL corpus and explicit export/import migration, with failure/restart evidence.
7. [PLUG-001.12 #356](https://github.com/callummarshall9/tsql-execution-engine/issues/356): remove SQL policy from the core and enforce measured API/dependency limits.

The [SYS-001 epic #357](https://github.com/callummarshall9/tsql-execution-engine/issues/357) owns protected engine catalogs,
master/model/tempdb/msdb lifecycles, authorization and schema migration. One authoritative catalog per database must survive
cutover; permanent dual writes and implicit raw-file compatibility are excluded. Whole-instance upgrade and recovery gate
final retirement. Backend replacement is explicit migration, not live hot-swap.

## Completion evidence

Canonical child issues carry their acceptance and dependency gates; this repository's coordination epic does not duplicate
those implementation stories. Package changes require exact provenance, package-consumer tests, locked/isolated restore,
conformance success/invalid/conflict/cancellation/disposal/recovery cases and applicable full build/test checks. Portability
requires two real adapters. The final architecture gate measures exported API/dependency reduction against the legacy
inventory and fails if SQL-specific policy leaks into the mandatory core. No runtime feature is credited by this document.

## Storage #79: extracted page core and retained compatibility

This delivery is the storage-source prerequisite for execution #356, tracked by #78. Neither parent is completed here.
Independent review and coordinator merge precede package publication; the downstream adapter adoption remains #356.

### Package and source boundary

- `SqlStorageEngine.Core` **1.0.0** depends only on exact `SqlExecutionEngine.Storage.Abstractions` **1.0.0** and the .NET BCL.
  `PageBackendFactory` and `PageBackendOptions` supply the approved `IBackendFactory` / `IBackendStore` ABI. No SQL catalog,
  row codec, authorization, graph referential policy or instance lifecycle is imported, initialized or evaluated by this path.
- `SqlStorageEngine.Compatibility` **1.17.0** retains the `sql-storage-engine.dll` assembly identity and existing SQL namespaces.
  It depends on Core 1.0.0 (NuGet minimum dependency; consumer locks pin the tested version). Do not reference the old
  `SqlStorageEngine` package and Compatibility together: they supply the same assembly. The existing 1.16.0 publication is
  unchanged. Update package composition explicitly; no runtime package substitution or live hot-swap is implied.
- Existing page I/O/checksums, cache, opaque physical identifiers, heap/overflow, B-tree/index, locking, WAL and recovery
  mechanisms move to Core. Existing physical APIs remain public solely to preserve legacy bindings; Compatibility forwards
  all 118 moved types. They are optional physical APIs, not additions to the mandatory neutral contract. SQL file headers,
  collations, rowversion/generated SQL values, catalog serialization, SQL transactions and legacy journal orchestration stay
  in Compatibility. In particular, `PageDatabase` is not the neutral store or a core dependency.
- Discovery source `b33a1f2bbcfc2ec743f8ac95e8b16655db4f4cba` exports **333** types. After extraction Compatibility defines
  **215**, Core defines **120** (118 retained physical types plus two factory/options types). The combined legacy API retains
  every baseline public declared member. The historical 196-type inventory describes the earlier published source and is
  not substituted for this measured baseline. Core's sorted public declared API has SHA-256
  `38BFDB8CBCF94BB721B78E23B3C64D57A1803609BD0FEA4FED40363ED2216C3C`.
  `CoreArchitectureTests` enforces that surface, dependency direction and SQL source exclusion. API growth requires review.

### Raw persistence, formats and guarantees

The bounded raw page store uses the extracted `FilePageStore`, page identity/checksum codecs and directory synchronization.
It writes a complete checksummed staging page image, flushes it, atomically replaces the same-directory live image and
synchronizes the directory. A separate persistent lock inode fences concurrent openers across replacement. The private
`PGC1` physical format and state magic are distinct from SQL files; neither path silently converts the other's data.
Each publication exclusively creates a unique, fixed-length `.page-stage-<nonce>.pending` sibling. Cleanup is
permitted only after that publication successfully created the file; opening a store never scans or deletes sidecars.
The conventional `<store>.pending` name is not reserved and may contain another store. Normal completion, cancellation
and exceptions remove the current writer's uncommitted stage. A killed writer can leave one bounded orphan image;
reopen ignores it and recovers exclusively from the published image. Orphans are conservatively retained for explicit
host-owned offline cleanup, because a filename or even a valid raw image does not prove ownership. Repeated process
termination can accumulate such files; the logical record/receipt limits are not a filesystem free-space quota.
Store creation synchronizes each ancestor directory entry, deepest first through the root, before publication, even
when ancestors already exist after an interrupted creator. Publication then synchronizes the leaf directory. Failure
to synchronize any ancestor aborts creation without publishing a store; retry repeats the complete synchronization chain.

Neutral state, fingerprinted operation receipts and opaque collection records share one published image. Admission is
published before the final data/receipt image. Recovery turns surviving Unknown admissions into durable Conflict before
new writes; published terminal receipts remain stable. An ambiguous publication quarantines the mount, returns Unknown,
and requires reopen. Before-admission cancellation has no effect; after admission the operation completes durably or reports
Unknown. Snapshot generations are mount scoped, while terminal receipts survive reopen. Snapshots use owned immutable
state, have explicit lifetime/quota/disposal, and do not change under later publication.

The profile is Linux local-filesystem durable rename plus directory-fsync, matching the existing platform admission for
explicit durability. Other operating systems fail admission; network/removable storage is not newly qualified. Bounds are
1–256 key bytes, 0–1,048,576 value bytes, 1–1,024 mutations and 4,194,304 encoded batch bytes; host limits are at most 8 MiB
charged record bytes, 1,024 lifetime receipts, 64 snapshots and one concurrent operation. Default snapshots are eight with
five-minute lifetime. State encoding is capped at 9 MiB. No optional accelerator is advertised. Whole-image copying is an
explicit cost; the extracted physical WAL remains available for existing compatibility behavior, not a false claim that the
new neutral publication uses WAL. The independent engine append-log adapter remains independently owned.

The neutral state machine/codec and unchanged shared conformance sources derive from execution source
`71ce61e704ff609f6936e66e7e1598364711f7ef` (LegacyAdapter and Storage.Conformance). Only persistence is replaced in the state
machine; tests register this page backend through a storage-local driver. Published Abstractions 1.0.0 records source
`5df368600eec5a78fa18d100d07668411d42df37`. Existing SQL files remain under their existing codecs and recovery rules.
Export/import into this new raw format is explicit downstream migration, never reinterpretation of the old adapter's SQL table.

### Validation and eight-part author review

1. **Delivered outcome:** actual physical source extraction, neutral raw publication and separately versioned compatibility;
   all 333 baseline public types/members retained, including binary forwarding. No execution #356 retirement credit.
2. **Learning:** SQL policy extended into PageDatabase and its header codec; moving those unchanged would not remove policy.
   The source baseline also exceeds the older published API inventory, so the comparison uses the actual discovery commit.
3. **Architecture:** core's sole package dependency is neutral contracts; Compatibility points to Core. SQL/domain authority
   stays above generic bytes. Physical compatibility exports do not enlarge the approved mandatory backend ABI.
4. **Debt/risk:** whole-image publication has bounded write/memory amplification and lifetime receipt exhaustion. Linux local
   filesystem semantics are required. The original and raw formats are distinct; downstream adoption must migrate explicitly.
5. **Story refinement:** storage #79 owns source/package preparation only. Publication follows independent review and merge;
   downstream adapter adoption, two-adapter SQL/migration proof and retirement remain execution #356.
6. **Roadmap/dependencies:** live recursive prerequisites of #262/#355/#372 were closed before coding; neither repository had
   overlapping open PRs. #78/#344/#356 remain open; unsupported coverage ownership is unchanged.
7. **Coverage/validation:** final Debug and Release full Unit suites, warnings as errors; shared nine-check conformance includes
   real child termination and real filesystem failure. Corruption/foreign SQL images are rejected without mutation. The exact
   8 MiB charged store survives reopen and one byte over has no receipt/effect. Package consumers use isolated locked restore,
   compare all baseline public members and execute a baseline-compiled binary against a baseline SQL file. No new SQL feature
   or two-provider portability is claimed. Final counts, source SHA, package bytes/hashes and coverage observations belong in
   the PR result, not a replacement inventory. Existing acceptance suites remain intact.
8. **Next work:** independent eight-part review, coordinator merge, sequential publication with fresh provenance, then resume
   execution #356 at its existing branch to adopt the raw factory and explicit compatibility package. Do not close #356/#78.

### Sequential publication after review and coordinator merge

No publication is performed by the author PR. On the reviewed merged source, verify the tree matches the accepted source
and retain its exact SHA. Use .NET 10, the committed locks and a private cache; do not override `Version` globally.

The restore step receives `NuGetPackageSourceCredentials_github` only in its runtime environment, using `github.actor`
and the job's short-lived `GITHUB_TOKEN` with Basic authentication. No password is stored in NuGet.config, artifacts or
source. The job's existing `contents: read` and `packages: write` permissions are retained because this job also publishes;
no broader token scope is added. Restore uses the exact `github` source key in the committed source mapping.

**Cross-repository prerequisite:** `SqlExecutionEngine.Storage.Abstractions` is a private package associated with
`callummarshall9/tsql-execution-engine`. Its package administrator must grant `callummarshall9/sql-storage-engine`
**Read** under the package's **Manage Actions access** settings before this repository's GITHUB_TOKEN can restore it.
Existing local injected credentials prove feed connectivity, not that Actions grant. The grant cannot be inferred from
repository admin permissions or a successful local PAT restore. If that package-level grant is unavailable, stop before
publication and have the coordinator resolve package access; do not make the package public or increase token scopes
implicitly. No publishing workflow is dispatched merely to test access. See the official
[GitHub NuGet authentication guidance](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry)
and [package Actions access controls](https://docs.github.com/en/packages/learn-github-packages/about-permissions-for-github-packages).


```sh
dotnet restore sql-storage-engine.sln --locked-mode
dotnet test sql-storage-engine.sln -c Release --no-restore -warnaserror -m:1
dotnet pack SqlStorageEngine.Core -c Release --no-restore -warnaserror -o artifacts
dotnet pack sql-storage-engine -c Release --no-restore -warnaserror -o artifacts
```

Inspect each nupkg's nuspec repository commit, version, dependencies and assembly contents; record SHA-256 and byte sizes.
Core 1.0.0 must contain no compatibility assembly, and Compatibility 1.17.0 must declare Core 1.0.0. Restore/run
`tests/PackageConsumer` against those artifacts in a clean private cache, then verify its generated lock with locked restore.
Build `tests/LegacyBinaryConsumer` with `LegacyAssemblyPath` pointing at the discovery-baseline assembly, run it once to
create a baseline SQL file, and pass baseline assembly, consumer DLL and SQL-file path to PackageConsumer. The consumer's
lock is local because it contains hashes of unpublished candidate packages; production dependency locks are committed.

After coordinator approval, tag the exact merged source `core-v1.0.0` and `compatibility-v1.17.0`. Dispatch `publish-nuget.yml`
on that source with version `1.17.0`; its locked restore/tests and independently versioned pack publish Core **first**, then
Compatibility, including symbols. The workflow also retains historical `v*.*.*` tag triggering; do not create such a tag
before approval. A retry must inspect any already published immutable package before continuing; never overwrite an existing
version or assume a duplicate means identical bytes. Download both publications to a fresh cache, verify source/hash and
repeat package-consumer/lock verification against the feed. Record exact published provenance for #356's return point.
The execution consumer must replace its SQL-table-based neutral persistence, explicitly pin packages, then run both real
adapters, portable migration, whole-instance recovery, legacy compatibility and its architecture/coverage gates before
retiring entry points. Source preparation alone does not satisfy those downstream gates.
