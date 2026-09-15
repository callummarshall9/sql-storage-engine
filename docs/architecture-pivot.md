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
