# Recovery

Startup validates WAL database identity and timeline before inspecting records. Analysis classifies transactions as committed, rolled back, or incomplete and records the first change LSN for each dirty page. Only a record envelope extending beyond final EOF is an incomplete tail; it is ignored and the WAL is truncated to the preceding checksum-valid boundary. Invalid framing, values, or checksums before that boundary are corruption and stop open.

Physical page-change records contain bounded full before- and after-images. Redo applies committed after-images only when their record LSN exceeds the stored page LSN, making replay idempotent. Page identity, type, image checksum, and image LSN are verified first. A corrupt current page may be repaired only from that verified full-page image; an invalid logged image stops recovery.

Undo processes incomplete transactions backward through their previous-LSN chains and restores verified before-images for heap, index, catalog, and overflow pages. Each completed action appends and flushes a compensation record linked to the next action requiring undo. A crash during undo therefore resumes behind already completed work. When the chain is exhausted, a rollback record is appended and flushed; committed transactions are never undone.

Checkpoint records contain a safe recovery-start LSN, active transaction IDs with their previous LSNs, and dirty pages with their first recovery LSNs. The record is appended and flushed before its discovery reference is atomically published; interrupted publication leaves the prior reference valid. WAL retention begins at the lesser of the safe start and every active transaction's required LSN, so required undo history is never removed.

Page checksum compatibility
---------------------------

Page checksum validation uses a lookup table for IEEE CRC-32, preserving the
existing page bytes and checksum-field exclusion. Compatibility tests use
independent zlib golden checksums for all supported page sizes and reject payload
and checksum-field corruption. The complete storage suite passes 532 tests under
an isolated output profile (`/tmp/sql-storage-crc-forward-tests.log`). Crash probes
and qualification documentation lookup derive paths from the running test
project, allowing custom output layouts without loading another build's probe.

Fresh current-source packaging also passes its consumer smoke test
(`/tmp/sql-crc-current-packaging.log`). The current CRC build passes 65 encrypted
TDS graph adoption assertions on fresh/restarted hosts
(`/tmp/sql-crc-graph-adoption-e2e.log`). The full E2E run from that isolated build
exits zero (`/tmp/sql-crc-full-suite.log`), including the positive graph fixture.
The current-source execution Fast suite also passes 2,426 tests
(`/tmp/sql-crc-engine-fast.log`).
