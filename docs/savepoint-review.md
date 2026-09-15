# TXN-001.04 storage savepoint review

The implementation adds transaction-owned typed positions and ordinal name lookup. Duplicate names shadow older live
positions; rollback to an older token invalidates only later positions, so name lookup can reveal an older duplicate.
Heap, index, catalog and allocation state come from the provider's checked before-image format. SQL admission is separate.

A root's exclusive lease and non-overlapping operation reservation cover creation and restoration. Snapshots are durable
internal files but never replace the root's recovery decision. A transient guard recovers the pre-operation image if
restoration fails. Caller cancellation before rewriting leaves the root unchanged; after rewriting begins, independent
cleanup budgets finish restoration/recovery. Failed recovery or sidecar cleanup dooms the root, permitting root rollback.
Terminal resolution and process recovery remove retained savepoint images. No execution-layer undo list is introduced.

The public interface uses default throwing implementations for the three new methods, preserving existing alternative
implementers' compatibility while the concrete engine supplies the exact supported contract. Each new type has its own
source file. Existing root commit/rollback and outcome receipts retain their semantics.

Validation includes duplicate/nested/repeated positions, foreign/forged/invalid tokens, 32-character/count boundaries,
aggregate byte quota, cancellation at creation and restoration boundaries, failed restore/recovery, operation overlap,
heap/index/catalog/file-length restoration and actual process termination before/during rollback and after root commit.
The resource test records 32 retained images and elapsed time without a timing gate. Linux-only Serializable ownership,
whole-file copying, 32 retained points, 256 MiB aggregate image ceiling and additional root/guard/buffer cost remain limits.

All 475 tests pass in Debug and Release with warnings as errors. The Release boundary observation retained
1,312,768 bytes for 32 images and took 19.074 ms; this is descriptive local evidence, not a performance threshold.
