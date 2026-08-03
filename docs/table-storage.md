# Table storage coordination

`TableStorage` is the logical mutation boundary between SQL execution and physical heap, overflow, and B+ tree storage. Callers submit a logical row once; they do not update indexes separately.

Insertion validates the complete row before allocation, writes required overflow chains, inserts the encoded heap row, and adds each catalog-derived `(IndexKey, RowId)` entry. Until transactions are available, a failure triggers reverse-order compensation: inserted index entries are removed, the heap row is deleted, and newly owned overflow chains are reclaimed. A `TableMutationException` preserves the initiating error and reports physical roots that cleanup could not reclaim. The returned generation-safe row ID is resolved through the same table API and decoded with overflow values restored.

Partial update retains the previous encoded row and its overflow ownership until the replacement is complete. An in-place update replaces only index keys whose logical values changed. If the heap requires relocation, every index entry is rewritten with the new `RowId`, including indexes whose key bytes did not change, before the old slot is deleted. Failure reverses index changes, restores the old heap bytes or deletes the unpublished relocated row, and frees newly allocated overflow chains.
