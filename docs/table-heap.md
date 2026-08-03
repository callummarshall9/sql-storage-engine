# Table heap and free-space map

A `TableHeap` is identified by its root `PageId`. Heap pages form a forward chain, with backward links recorded for diagnostics and later maintenance. Every access uses a short-lived buffer-pool pin. Insertion releases the current pin before advancing or allocating, and scanning copies live rows before releasing a page and yielding them in page/slot order.

Lookup is restricted to pages in the table chain and distinguishes unknown pages, unknown slots, deleted slots, and stale generations. Cyclic or inaccessible links are corruption failures. The table heap does not own its buffer pool or allocator.

## Volatile free-space hints

`InMemoryFreeSpaceMap` stores an exact last-observed free-byte count plus one of five coarse categories (`None`, `Tiny`, `Small`, `Medium`, or `Large`). Candidate selection is deterministic by numeric page ID. Entries are hints only:

- insertion pins and verifies a candidate before changing it;
- an optimistic stale entry is corrected or removed after verification;
- an empty or pessimistic map falls back to walking the heap chain;
- insert, update, delete, and compaction refresh the affected page;
- `TableHeap.OpenAsync` rebuilds all entries from validated page headers.

The map is intentionally not persisted. Losing it affects insertion search cost, never row correctness.
