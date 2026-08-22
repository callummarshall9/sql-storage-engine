# Table storage coordination

`TableStorage` is the logical mutation boundary between SQL execution and physical heap, overflow, and B+ tree storage. Callers submit a logical row once; they do not update indexes separately.

Insertion validates the complete row before allocation, writes required overflow chains, inserts the encoded heap row, and adds each catalog-derived `(IndexKey, RowId)` entry. Until transactions are available, a failure triggers reverse-order compensation: inserted index entries are removed, the heap row is deleted, and newly owned overflow chains are reclaimed. A `TableMutationException` preserves the initiating error and reports physical roots that cleanup could not reclaim. The returned generation-safe row ID is resolved through the same table API and decoded with overflow values restored.

Partial update retains the previous encoded row and its overflow ownership until the replacement is complete. An in-place update replaces only index keys whose logical values changed. If the heap requires relocation, every index entry is rewritten with the new `RowId`, including indexes whose key bytes did not change, before the old slot is deleted. Failure reverses index changes, restores the old heap bytes or deletes the unpublished relocated row, and frees newly allocated overflow chains.

Deletion first reads the logical row and its owned overflow references, removes every derived index entry, and only then invalidates the heap slot and advances its generation. Any failure before heap deletion restores already removed index entries and throws with cleanup details. Overflow chains are reclaimed after logical deletion; a failure there is returned in `DeferredCleanupPageIds`, so deletion is never silently reported as fully reclaimed.

The public `IStorageTable.SampleAsync` boundary accepts either `StoragePercentTableSample` or
`StorageRowsTableSample`. `TableStorage` delegates selection to the heap before decoding selected rows and projecting
the current column set. The engine wrapper holds the same statement gate used by ordinary scans, applies optional
`StorageReadOptions` masking after decoding, and releases the gate on completion, cancellation, error, or early consumer
disposal. Sampling is read-only and does not change row identities, heap state, catalog metadata, or indexes.
## System-versioned tables

`CreateSystemVersionedTableAsync` creates and publishes the current and history heaps as one catalog change. The current
record identifies its history table and exact non-null `datetime2` `ROW START`/`ROW END` columns; catalog validation
requires the history table's ordered logical schema to match. Public history handles are read-only.

Inserts generate `[current UTC time, datetime2 maximum)`. Updates archive the old row with an end equal to the new
current row's start, and deletes archive the old row before removing it. Direct updates/deletes run inside the same
durable statement-journal boundary used by `ExecuteStatementAsync`; failure or crash cannot publish only one side of the
current/history mutation.

`TemporalScanAsync` combines the two heaps without disguising one as the other: each `StorageTemporalRow` contains its
source `TableId` and generation-safe `RowId`. Typed AS OF, FROM/TO, BETWEEN/AND, CONTAINED IN, and ALL descriptors implement
their distinct half-open/inclusive SQL boundaries. Scans preserve cancellation, masking, statement coordination, and
reopen behavior.
