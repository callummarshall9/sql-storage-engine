using sql_storage_engine.Buffers;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Overflow;
using sql_storage_engine.Pages;
using sql_storage_engine.Rows;
using sql_storage_engine.Tables;

namespace sql_storage_engine;

/// <summary>
/// Owns a database file and exposes logical catalog and row operations. Consumers should depend on
/// <see cref="IStorageEngine"/>, <see cref="IStorageCatalog"/>, and <see cref="IStorageTable"/>.
/// </summary>
public sealed class StorageEngine : IStorageEngine, IStorageCatalog
{
    private readonly PageDatabase _database;
    private readonly BufferPool _bufferPool;
    private readonly CatalogService _catalog;
    private readonly OverflowManager _overflow;
    private readonly int _inlineValueThreshold;
    private bool _disposed;

    private StorageEngine(PageDatabase database, BufferPool bufferPool, CatalogService catalog,
        StorageEngineOptions options)
    {
        _database = database;
        _bufferPool = bufferPool;
        _catalog = catalog;
        _overflow = new OverflowManager(bufferPool, database);
        _inlineValueThreshold = options.InlineValueThreshold;
    }

    public IStorageCatalog Catalog => this;
    public IReadOnlyList<CatalogTable> Tables => _catalog.Tables;
    public IReadOnlyList<CatalogIndex> Indexes => _catalog.Indexes;

    public static async ValueTask<StorageEngine> CreateAsync(string path, StorageEngineOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new StorageEngineOptions();
        Validate(options);
        var database = await PageDatabase.CreateAsync(path, options.PageSize, cancellationToken).ConfigureAwait(false);
        try
        {
            var pool = new BufferPool(database, options.BufferPoolCapacity, leaveOpen: true);
            return new StorageEngine(database, pool, CatalogService.CreateEmpty(database, database, pool), options);
        }
        catch { await database.DisposeAsync().ConfigureAwait(false); throw; }
    }

    public static async ValueTask<StorageEngine> OpenAsync(string path, StorageEngineOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new StorageEngineOptions();
        Validate(options);
        var database = await PageDatabase.OpenAsync(path, cancellationToken).ConfigureAwait(false);
        BufferPool? pool = null;
        try
        {
            pool = new BufferPool(database, options.BufferPoolCapacity, leaveOpen: true);
            var catalog = database.Header.CatalogRootPageId is { } root
                ? await CatalogService.OpenAsync(root, database, database, pool, cancellationToken).ConfigureAwait(false)
                : CatalogService.CreateEmpty(database, database, pool);
            return new StorageEngine(database, pool, catalog, options);
        }
        catch
        {
            if (pool is not null) await pool.DisposeAsync().ConfigureAwait(false);
            await database.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public bool TryGetTable(string name, out CatalogTable? table) => _catalog.TryOpenTable(name, out table);
    public bool TryGetTable(TableId id, out CatalogTable? table) => _catalog.TryOpenTable(id, out table);
    public bool TryGetIndex(TableId tableId, string name, out CatalogIndex? index) =>
        _catalog.TryOpenIndex(name, tableId, out index);
    public IReadOnlyList<CatalogIndex> GetIndexes(TableId tableId) =>
        _catalog.Indexes.Where(index => index.TableId == tableId).ToArray();

    public async ValueTask<CatalogTable> CreateTableAsync(string name, IEnumerable<CatalogColumn> columns,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var table = await _catalog.CreateTableAsync(name, 1, columns, cancellationToken).ConfigureAwait(false);
        await PublishCatalogAsync(cancellationToken).ConfigureAwait(false);
        return table;
    }

    public async ValueTask<CatalogIndex> CreateIndexAsync(string name, TableId tableId, bool isUnique,
        IEnumerable<CatalogIndexedColumn> columns, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var index = await _catalog.CreateIndexAsync(name, tableId, isUnique, columns, cancellationToken).ConfigureAwait(false);
        await PublishCatalogAsync(cancellationToken).ConfigureAwait(false);
        return index;
    }

    public async ValueTask<IStorageTable> OpenTableAsync(TableId tableId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!_catalog.TryOpenTable(tableId, out var definition))
            throw new KeyNotFoundException($"Unknown table {tableId}.");
        var heap = await _catalog.OpenHeapAsync(definition!, cancellationToken).ConfigureAwait(false);
        var indexes = _catalog.Indexes.Where(index => index.TableId == tableId)
            .Select(index => new TableIndex(index, _catalog.OpenIndex(index))).ToArray();
        var storage = new TableStorage(definition!, heap,
            new OverflowRowCodec(_overflow, _inlineValueThreshold), _overflow, indexes);
        return new StorageTable(storage, this);
    }

    public ValueTask<IStorageIndex> OpenIndexAsync(IndexId indexId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        var index = _catalog.Indexes.SingleOrDefault(candidate => candidate.Id == indexId)
            ?? throw new KeyNotFoundException($"Unknown index {indexId}.");
        var table = _catalog.Tables.Single(candidate => candidate.Id == index.TableId);
        return ValueTask.FromResult<IStorageIndex>(new StorageIndex(index, table, _catalog.OpenIndex(index)));
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    { ThrowIfDisposed(); return _bufferPool.FlushAllAsync(cancellationToken); }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _bufferPool.DisposeAsync().ConfigureAwait(false);
        await _database.DisposeAsync().ConfigureAwait(false);
    }

    private async ValueTask PublishCatalogAsync(CancellationToken cancellationToken)
    {
        if (_catalog.RootPageId is not { } root) throw new InvalidOperationException("Catalog publication produced no root.");
        await _database.PublishCatalogRootAsync(root, cancellationToken).ConfigureAwait(false);
    }

    private static void Validate(StorageEngineOptions options)
    {
        if (!PageConstants.IsSupportedSize(options.PageSize)) throw new ArgumentOutOfRangeException(nameof(options.PageSize));
        if (options.BufferPoolCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(options.BufferPoolCapacity));
        if (options.InlineValueThreshold < 0 || options.InlineValueThreshold > RowCodec.MaximumInlineValueLength)
            throw new ArgumentOutOfRangeException(nameof(options.InlineValueThreshold));
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed class StorageTable(TableStorage storage, StorageEngine owner) : IStorageTable
    {
        public CatalogTable Definition => storage.Definition;
        public async ValueTask<RowId> InsertAsync(Row row, CancellationToken cancellationToken = default)
        { var id = await storage.InsertAsync(row, cancellationToken).ConfigureAwait(false); await owner.FlushAndPublishAsync(cancellationToken).ConfigureAwait(false); return id; }
        public async ValueTask<StoredRow?> GetAsync(RowId rowId, CancellationToken cancellationToken = default)
        { var result = await storage.TryGetAsync(rowId, cancellationToken).ConfigureAwait(false); return result.Found ? new StoredRow(rowId, result.Row!) : null; }
        public IAsyncEnumerable<StoredRow> ScanAsync(CancellationToken cancellationToken = default) => storage.ScanAsync(cancellationToken);
        public async ValueTask<TableUpdateResult> UpdateAsync(RowId rowId, RowUpdate update, CancellationToken cancellationToken = default)
        { var result = await storage.UpdateAsync(rowId, update, cancellationToken).ConfigureAwait(false); await owner.FlushAndPublishAsync(cancellationToken).ConfigureAwait(false); return result; }
        public async ValueTask<TableDeleteResult> DeleteAsync(RowId rowId, CancellationToken cancellationToken = default)
        { var result = await storage.DeleteAsync(rowId, cancellationToken).ConfigureAwait(false); await owner.FlushAndPublishAsync(cancellationToken).ConfigureAwait(false); return result; }
    }

    private sealed class StorageIndex(CatalogIndex definition, CatalogTable table, PersistentBPlusTree tree) : IStorageIndex
    {
        public CatalogIndex Definition => definition;

        public ValueTask<IReadOnlyList<RowId>> FindAsync(IReadOnlyList<SqlValue> values,
            CancellationToken cancellationToken = default) =>
            tree.FindAsync(CatalogIndexKey.EncodeValues(values, table, definition), cancellationToken);

        public async IAsyncEnumerable<RowId> ScanAsync(IReadOnlyList<SqlValue> lowerBound,
            IReadOnlyList<SqlValue> upperBound, bool includeLowerBound = true, bool includeUpperBound = true,
            ScanDirection direction = ScanDirection.Ascending,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var range = new IndexRange(CatalogIndexKey.EncodeValues(lowerBound, table, definition),
                CatalogIndexKey.EncodeValues(upperBound, table, definition), includeLowerBound, includeUpperBound, direction);
            await foreach (var entry in tree.ScanAsync(range, cancellationToken).ConfigureAwait(false))
                yield return entry.RowId;
        }
    }

    private async ValueTask FlushAndPublishAsync(CancellationToken cancellationToken)
    {
        await _bufferPool.FlushAllAsync(cancellationToken).ConfigureAwait(false);
        if (_catalog.RootPageId is { } root && _database.Header.CatalogRootPageId != root)
            await _database.PublishCatalogRootAsync(root, cancellationToken).ConfigureAwait(false);
    }
}
