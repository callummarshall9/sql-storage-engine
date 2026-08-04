using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Tables;

namespace sql_storage_engine;

/// <summary>A row and its opaque, generation-safe physical identity.</summary>
public sealed record StoredRow(RowId RowId, Row Row);

/// <summary>Options controlling the storage engine's bounded in-memory resources.</summary>
public sealed record StorageEngineOptions
{
    public int PageSize { get; init; } = Pages.PageConstants.DefaultSize;
    public int BufferPoolCapacity { get; init; } = 256;
    public int InlineValueThreshold { get; init; } = 1024;
}

/// <summary>Read-only catalog operations intended for name binding and semantic analysis.</summary>
public interface IStorageCatalog
{
    IReadOnlyList<CatalogTable> Tables { get; }
    IReadOnlyList<CatalogIndex> Indexes { get; }
    bool TryGetTable(string name, out CatalogTable? table);
    bool TryGetTable(TableId id, out CatalogTable? table);
    bool TryGetIndex(TableId tableId, string name, out CatalogIndex? index);
    IReadOnlyList<CatalogIndex> GetIndexes(TableId tableId);
}

/// <summary>Logical table access for a SQL executor; physical pages and encodings remain hidden.</summary>
public interface IStorageTable
{
    CatalogTable Definition { get; }
    ValueTask<RowId> InsertAsync(Row row, CancellationToken cancellationToken = default);
    ValueTask<StoredRow?> GetAsync(RowId rowId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<StoredRow> ScanAsync(CancellationToken cancellationToken = default);
    ValueTask<TableUpdateResult> UpdateAsync(RowId rowId, RowUpdate update,
        CancellationToken cancellationToken = default);
    ValueTask<TableDeleteResult> DeleteAsync(RowId rowId, CancellationToken cancellationToken = default);
}

/// <summary>Logical secondary-index access using typed values in the index's declared column order.</summary>
public interface IStorageIndex
{
    CatalogIndex Definition { get; }
    ValueTask<IReadOnlyList<RowId>> FindAsync(IReadOnlyList<SqlValue> values,
        CancellationToken cancellationToken = default);
    IAsyncEnumerable<RowId> ScanAsync(IReadOnlyList<SqlValue> lowerBound,
        IReadOnlyList<SqlValue> upperBound, bool includeLowerBound = true, bool includeUpperBound = true,
        ScanDirection direction = ScanDirection.Ascending, CancellationToken cancellationToken = default);
}

/// <summary>The supported high-level boundary between a SQL engine and this storage package.</summary>
public interface IStorageEngine : IAsyncDisposable
{
    IStorageCatalog Catalog { get; }
    ValueTask<CatalogTable> CreateTableAsync(string name, IEnumerable<CatalogColumn> columns,
        CancellationToken cancellationToken = default);
    ValueTask<CatalogIndex> CreateIndexAsync(string name, TableId tableId, bool isUnique,
        IEnumerable<CatalogIndexedColumn> columns, CancellationToken cancellationToken = default);
    ValueTask<IStorageTable> OpenTableAsync(TableId tableId, CancellationToken cancellationToken = default);
    ValueTask<IStorageIndex> OpenIndexAsync(IndexId indexId, CancellationToken cancellationToken = default);
    ValueTask FlushAsync(CancellationToken cancellationToken = default);
}
