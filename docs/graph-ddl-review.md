# Atomic graph creation — 1.12.0

## Implementation review

IStorageStatement adds CreateGraphNodeTableAsync and CreateGraphEdgeTableAsync. A dedicated internal builder owns the
hidden trailing columns, unique identity index, both edge adjacency indexes and registration. All stages run under the
existing statement gate and before-image journal. Failed graph DDL poisons the statement even when its exception is
caught inside the callback; success publishes the latest catalog before marking the journal committed.

## Learnings

Calling the old root registration API from a statement would re-enter its semaphore and deadlock. The new builder uses
catalog registration under the already-owned statement boundary. Composing the former root methods outside this boundary
would still permit partial effects. The public API exposes only typed creation, never an arbitrary catalog callback.

## Architecture and compatibility

Each new type has its own file. No public page/index implementation dependency or catalog format change is introduced.
Catalog format 10 and existing graph readers remain compatible. IStorageStatement implementers must add the two methods
when recompiling. Per-engine internal publication observers support deterministic fault and durable-image recovery tests;
no observer is exposed in the package's public API. Existing graph DML contracts and identity semantics are unchanged.

## Debt and supported scope

Creation is restricted to new tables. Populated or empty ordinary-table conversion rejects through duplicate-name
validation. Payloads cannot forge generated columns or reserved names. One ordered endpoint-table pair is intrinsic,
unnamed and mandatory, with NO ACTION node-deletion behavior; no standalone named/multiple constraint catalog, CASCADE,
ALTER/DROP/rename or permission principal is exposed. These are explicit unsupported operations for the SQL graph-DDL
design, not silent approximations. Calls are trusted host operations. Concurrent root operations/read snapshots are
outside the existing statement contract; clients must await statement operations and coordinate access.

## Refactoring review

The change is additive: two interface methods, a small builder, statement failure state and a commit-time poison check.
Existing root graph registration and DML implementations remain unchanged. Tests inject faults after persisted table,
identity-index, outgoing-index, incoming-index and registration boundaries, without replacing the underlying storage work.

## Roadmap review

This release supplies the atomic new-graph-table prerequisite for execution GRAPH-001.06. That enabler must still record
exact SQL connection-constraint/lifecycle decisions, authorization requirements and rejection behavior before SQL mapping.
No broader DDL or relational lowering is implied by the provider capability. Publication is through the existing tag-triggered
NuGet workflow after local verification; downstream adoption must verify the published package's source and contract.

## Coverage and failure review

Twenty-four focused graph-DDL tests cover committed reopen and usable identities, hidden/index ownership, distinct endpoint
roles, invalid and populated requests, expired scopes, caught failures, cancellation and retry. Every publication boundary
is tested for rollback and with a copied durable database/active-journal image reopened independently. Those image tests
simulate interruption at exact persistence points; they do not claim a hardware power-loss test. Existing corruption,
allocation, I/O, format compatibility and graph-DML tests remain enabled.

## Performance review

[Retained baseline](graph-ddl-baseline.json) records 1/5/10 node/edge pairs created through separate atomic statements,
validating all tables and indexes, with elapsed ticks, process allocation deltas and database size. These are descriptive
host-specific costs, without a timing gate. The existing journal copies the database per statement; large catalogs remain
an explicit scalability limit. Reproduce with STORAGE112_BASELINE_OUTPUT set to an absolute output path and the Release
GraphDdlBaselineTests filter.

## Validation

Validation on 2026-09-13: 24 focused GraphDdl tests and 432 full tests in each Release/Debug configuration passed,
zero failures/skips. Both full runs used `--no-restore --disable-build-servers -m:1 -nodeReuse:false
-p:UseSharedCompilation=false -warnaserror`. Release packing with warnings as errors produced 1.12.0 nupkg and symbols.
Focused formatting and `git diff --check` passed. The retained Release baseline ran with all table/index assertions.
No test or acceptance requirement was weakened. Existing untracked 1.10.0/1.11.0 artifacts are preserved and excluded
from source control. New binary artifacts are also excluded; the release workflow packs from the annotated source tag.
