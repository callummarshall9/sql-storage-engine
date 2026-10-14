// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Indexes;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Indexes;

namespace sql_storage_engine.Exposures.Indexes;
public interface IIndexTreeExposure
{
    IndexTreeBinding ReadIndexTreeBinding();

    PageId ReadRootPageId();

    bool ReadIsUnique();

    ValueTask<bool> RemoveAsync(IndexKey indexKey, RowId rowId, CancellationToken cancellationToken = default);

    ValueTask<IndexDeleteResult> DeleteAsync(IndexKey deletedIndexKey, RowId deletedRowId, CancellationToken cancellationToken = default);

    ValueTask<IndexInsertResult> InsertWithoutSplitAsync(IndexKey newIndexKey, RowId newRowId, CancellationToken cancellationToken = default);

    ValueTask<IndexInsertResult> InsertWithoutSplitAsync(IndexKey newIndexKey, RowId newRowId, ReadOnlyMemory<byte> includedPayload, CancellationToken cancellationToken = default);

    ValueTask InsertAsync(IndexKey newIndexKey, RowId newRowId, CancellationToken cancellationToken = default);

    ValueTask InsertAsync(IndexKey newIndexKey, RowId newRowId, ReadOnlyMemory<byte> includedPayload, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<RowId>> FindAsync(IndexKey indexKey, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<LeafIndexEntry>> FindEntriesAsync(IndexKey indexKey, CancellationToken cancellationToken = default);

    IAsyncEnumerable<LeafIndexEntry> ScanAsync(IndexRange indexRange, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<PageId>> GetLeafPageIdsAsync(CancellationToken cancellationToken = default);
}