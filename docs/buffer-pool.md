# Buffer pool

The buffer pool owns a fixed number of page-sized frames and, unless constructed with `leaveOpen: true`, its backing `IPageStore`. Callers own each `IPinnedPage` until it is disposed. Disposal is idempotent, releases exactly one pin, and invalidates further access through that handle. A pool cannot be disposed while a pin remains live.

## Concurrency and replacement

One asynchronous gate protects the page table, cache loads, clock hand, eviction, and flushing. This deliberately favors a small, auditable first implementation. Concurrent misses for the same page are serialized, so only one frame and one store read are created. The clock ring gives referenced pages one second chance, skips pinned frames, and reports `StorageResourceExhaustedException` after two complete scans when no victim exists. Reassigning a frame clears its dirty state, page LSN, dirty generation, and prior reference history.

## Dirty pages and flushing

Mutators must call `MarkDirty` with the page LSN after changing page memory. Dirty eviction writes one complete snapshot before reassigning the frame. A failed guard or store write leaves the original frame cached and dirty. `FlushPageAsync` and `FlushAllAsync` include pinned pages: they write a snapshot without releasing the caller's pin. A dirty-generation check prevents completion of an older write from clearing a newer dirty mark.

`IPageFlushGuard` is invoked before every dirty write. Its current default is a cancellation-aware no-op; the WAL milestone can inject a guard that first makes the page's LSN durable. Full and page-specific explicit flushes finish with the backing store's durable flush operation.

Cache hit and miss counters are exposed per pool and emitted through the `sql-storage-engine.buffer-pool` `Meter` as `buffer_pool.hits` and `buffer_pool.misses`. Page contents are never included in metrics.
