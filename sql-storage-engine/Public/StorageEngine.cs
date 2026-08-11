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
    public IReadOnlyList<CatalogScalarType> ScalarTypes => _catalog.ScalarTypes;
    public IReadOnlyList<CatalogTableType> TableTypes => _catalog.TableTypes;
    public IReadOnlyList<SqlXmlSchemaCollection> XmlSchemaCollections => _catalog.XmlSchemaCollections;
    public IReadOnlyList<CatalogAssembly> Assemblies => _catalog.Assemblies;
    public string DefaultCollation => _database.Header.DefaultCollation;

    public static async ValueTask<StorageEngine> CreateAsync(string path, StorageEngineOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new StorageEngineOptions();
        Validate(options);
        var database = await PageDatabase.CreateAsync(path, options.PageSize, options.DefaultCollation, cancellationToken).ConfigureAwait(false);
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
    public bool TryGetScalarType(string schemaName, string name, out CatalogScalarType? type) =>
        _catalog.TryOpenScalarType(schemaName, name, out type);
    public bool TryGetTableType(string schemaName, string name, out CatalogTableType? type) =>
        _catalog.TryOpenTableType(schemaName, name, out type);
    public bool TryGetXmlSchemaCollection(string schemaName, string name, out SqlXmlSchemaCollection? collection) =>
        _catalog.TryOpenXmlSchemaCollection(schemaName, name, out collection);
    public bool TryGetAssembly(string name, out CatalogAssembly? assembly) => _catalog.TryOpenAssembly(name, out assembly);

    public async ValueTask<CatalogTable> CreateTableAsync(string name, IEnumerable<CatalogColumn> columns,
        CancellationToken cancellationToken = default)
        => await CreateTableAsync(name, columns, [], cancellationToken).ConfigureAwait(false);

    public async ValueTask<CatalogTable> CreateTableAsync(string name, IEnumerable<CatalogColumn> columns,
        IEnumerable<CatalogCheckConstraint> checkConstraints, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var table = await _catalog.CreateTableAsync(name, 1, ApplyDatabaseCollation(columns), checkConstraints, cancellationToken).ConfigureAwait(false);
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

    public async ValueTask<CatalogIndex> CreateIndexAsync(string name, TableId tableId, bool isUnique,
        IEnumerable<CatalogIndexedColumn> columns, CatalogBTreeIndexOptions options,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var index = await _catalog.CreateIndexAsync(name, tableId, isUnique, columns, options, cancellationToken).ConfigureAwait(false);
        await PublishCatalogAsync(cancellationToken).ConfigureAwait(false); return index;
    }

    public async ValueTask<CatalogIndex> CreateSpecializedIndexAsync(string name, TableId tableId,
        CatalogIndexedColumn column, CatalogSpecializedIndexOptions options,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var index = await _catalog.CreateSpecializedIndexAsync(name, tableId, column, options, cancellationToken).ConfigureAwait(false);
        await PublishCatalogAsync(cancellationToken).ConfigureAwait(false);
        return index;
    }

    public async ValueTask<CatalogScalarType> CreateScalarTypeAsync(SqlType definition,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var type = await _catalog.CreateScalarTypeAsync(definition.ApplyDefaultCollation(DefaultCollation), cancellationToken).ConfigureAwait(false);
        await PublishCatalogAsync(cancellationToken).ConfigureAwait(false);
        return type;
    }

    public async ValueTask<CatalogTableType> CreateTableTypeAsync(string schemaName, string name,
        IEnumerable<CatalogColumn> columns, IEnumerable<CatalogTableTypeIndex>? indexes = null,
        CancellationToken cancellationToken = default)
        => await CreateTableTypeAsync(schemaName, name, columns, indexes, null, false, cancellationToken).ConfigureAwait(false);

    public async ValueTask<CatalogTableType> CreateTableTypeAsync(string schemaName, string name,
        IEnumerable<CatalogColumn> columns, IEnumerable<CatalogTableTypeIndex>? indexes,
        IEnumerable<CatalogCheckConstraint>? checkConstraints, bool isMemoryOptimized,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var type = await _catalog.CreateTableTypeAsync(schemaName, name, ApplyDatabaseCollation(columns), indexes, checkConstraints,
            isMemoryOptimized, cancellationToken)
            .ConfigureAwait(false);
        await PublishCatalogAsync(cancellationToken).ConfigureAwait(false);
        return type;
    }

    public async ValueTask<SqlXmlSchemaCollection> CreateXmlSchemaCollectionAsync(SqlXmlSchemaCollection collection,
        CancellationToken cancellationToken = default)
    { var result = await _catalog.CreateXmlSchemaCollectionAsync(collection, cancellationToken).ConfigureAwait(false); await PublishCatalogAsync(cancellationToken).ConfigureAwait(false); return result; }

    public async ValueTask<SqlXmlSchemaCollection> AlterXmlSchemaCollectionAsync(string schemaName, string name,
        IEnumerable<string> additionalDefinitions, CancellationToken cancellationToken = default)
    { var result = await _catalog.AlterXmlSchemaCollectionAsync(schemaName, name, additionalDefinitions, cancellationToken).ConfigureAwait(false); await PublishCatalogAsync(cancellationToken).ConfigureAwait(false); return result; }

    public async ValueTask DropXmlSchemaCollectionAsync(string schemaName, string name,
        CancellationToken cancellationToken = default)
    { await _catalog.DropXmlSchemaCollectionAsync(schemaName, name, cancellationToken).ConfigureAwait(false); await PublishCatalogAsync(cancellationToken).ConfigureAwait(false); }

    public async ValueTask<CatalogAssembly> CreateAssemblyAsync(CatalogAssembly assembly,
        CancellationToken cancellationToken = default)
    { var result = await _catalog.CreateAssemblyAsync(assembly, cancellationToken).ConfigureAwait(false); await PublishCatalogAsync(cancellationToken).ConfigureAwait(false); return result; }

    public async ValueTask DropAssemblyAsync(string name, CancellationToken cancellationToken = default)
    { await _catalog.DropAssemblyAsync(name, cancellationToken).ConfigureAwait(false); await PublishCatalogAsync(cancellationToken).ConfigureAwait(false); }

    private IEnumerable<CatalogColumn> ApplyDatabaseCollation(IEnumerable<CatalogColumn> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        return columns.Select(column => new CatalogColumn(column.Id, column.Name,
            column.Type.ApplyDefaultCollation(DefaultCollation), column.IsNullable, column.DefaultExpression,
            column.Identity, column.IsRowGuidCol, column.ComputedExpression, column.IsComputedPersisted,
            column.IsSparse, column.IsColumnSet, column.IsFileStream, column.MaskingFunction, column.Encryption,
            column.GeneratedAlways, column.IsHidden));
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
            new OverflowRowCodec(_overflow, _inlineValueThreshold), _overflow, indexes,
            _database.AllocateRowVersionAsync,
            async token =>
            {
                var value = await _catalog.AllocateIdentityAsync(tableId, token).ConfigureAwait(false);
                // IDENTITY values are consumed even when later expression/check/index validation rejects the row.
                await PublishCatalogAsync(token).ConfigureAwait(false);
                return value;
            },
            _database.AllocateGeneratedValueAsync,
            () => _catalog.Indexes.Where(index => index.TableId == tableId)
                .Select(index => new TableIndex(index, _catalog.OpenIndex(index))).ToArray());
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
        return ValueTask.FromResult<IStorageIndex>(new StorageIndex(index, table, _catalog.OpenIndex(index), this));
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
        _ = SqlCollation.Parse(options.DefaultCollation);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed class StorageTable(TableStorage storage, StorageEngine owner) : IStorageTable
    {
        public CatalogTable Definition => storage.Definition;
        public async ValueTask<RowId> InsertAsync(Row row, CancellationToken cancellationToken = default)
        { var id = await storage.InsertAsync(row, cancellationToken).ConfigureAwait(false); await owner.FlushAndPublishAsync(cancellationToken).ConfigureAwait(false); return id; }
        public async ValueTask<TableInsertResult> TryInsertAsync(Row row, CancellationToken cancellationToken = default)
        { var result = await storage.TryInsertAsync(row, cancellationToken).ConfigureAwait(false); await owner.FlushAndPublishAsync(cancellationToken).ConfigureAwait(false); return result; }
        public async ValueTask<StoredRow?> GetAsync(RowId rowId, CancellationToken cancellationToken = default)
        { var result = await storage.TryGetAsync(rowId, cancellationToken).ConfigureAwait(false); return result.Found ? new StoredRow(rowId, result.Row!) : null; }
        public async ValueTask<StoredRow?> GetAsync(RowId rowId, StorageReadOptions readOptions,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(readOptions);
            var result = await storage.TryGetAsync(rowId, cancellationToken).ConfigureAwait(false);
            return result.Found ? new StoredRow(rowId, ApplyMasking(result.Row!, readOptions)) : null;
        }
        public IAsyncEnumerable<StoredRow> ScanAsync(CancellationToken cancellationToken = default) => storage.ScanAsync(cancellationToken);
        public async IAsyncEnumerable<StoredRow> ScanAsync(StorageReadOptions readOptions,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(readOptions);
            await foreach (var stored in storage.ScanAsync(cancellationToken).ConfigureAwait(false))
                yield return stored with { Row = ApplyMasking(stored.Row, readOptions) };
        }
        public async ValueTask<TableUpdateResult> UpdateAsync(RowId rowId, RowUpdate update, CancellationToken cancellationToken = default)
        { var result = await storage.UpdateAsync(rowId, update, cancellationToken).ConfigureAwait(false); await owner.FlushAndPublishAsync(cancellationToken).ConfigureAwait(false); return result; }
        public async ValueTask<TableDeleteResult> DeleteAsync(RowId rowId, CancellationToken cancellationToken = default)
        { var result = await storage.DeleteAsync(rowId, cancellationToken).ConfigureAwait(false); await owner.FlushAndPublishAsync(cancellationToken).ConfigureAwait(false); return result; }

        private Row ApplyMasking(Row row, StorageReadOptions readOptions) => new(row.Values.Select((value, position) =>
        {
            var column = Definition.Columns[position];
            return readOptions.CanUnmask(column.Id) ? value : SqlDataMasking.Apply(column, value);
        }));
    }

    private sealed class StorageIndex(CatalogIndex definition, CatalogTable table, PersistentBPlusTree tree,
        StorageEngine owner) : IStorageIndex
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

        public async ValueTask<IReadOnlyList<SpecializedIndexMatch>> SearchNearestAsync(SqlValue query, int count,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (definition.Method is not (CatalogIndexMethod.Spatial or CatalogIndexMethod.Vector))
                throw new InvalidOperationException("Nearest-neighbor search requires a spatial or vector index.");
            var columnId = definition.Columns.Single().ColumnId;
            var sourceColumn = table.Columns.Select((column, position) => (column, position))
                .Single(item => item.column.Id == columnId);
            if (query.IsNull) throw new ArgumentException("Nearest-neighbor query values cannot be NULL.", nameof(query));
            sourceColumn.column.Type.Validate(query, "query");
            var columnPosition = sourceColumn.position;
            var storage = await owner.OpenTableAsync(table.Id, cancellationToken).ConfigureAwait(false);
            List<SpecializedIndexMatch> matches = [];
            var range = new IndexRange(new IndexKey([0]), new IndexKey([2]));
            await foreach (var entry in tree.ScanAsync(range, cancellationToken).ConfigureAwait(false))
            {
                var stored = await storage.GetAsync(entry.RowId, cancellationToken).ConfigureAwait(false);
                if (stored is null || stored.Row.Values[columnPosition].IsNull) continue;
                var distance = (definition.Method, query, stored.Row.Values[columnPosition]) switch
                {
                    (CatalogIndexMethod.Spatial, SpatialSqlValue requested, SpatialSqlValue candidate) =>
                        requested.Value.STDistance(candidate.Value) ?? double.NaN,
                    (CatalogIndexMethod.Vector, VectorSqlValue requested, VectorSqlValue candidate) =>
                        requested.DistanceTo(candidate, definition.SpecializedOptions!.VectorMetric!.Value),
                    _ => throw new ArgumentException("Query value does not match the specialized index type.", nameof(query))
                };
                if (!double.IsFinite(distance)) continue;
                matches.Add(new SpecializedIndexMatch(entry.RowId, distance));
            }
            return matches.OrderBy(match => match.Distance).ThenBy(match => match.RowId.PageId.Value)
                .ThenBy(match => match.RowId.SlotId.Value).Take(count).ToArray();
        }

        public async ValueTask<IReadOnlyList<RowId>> FindJsonPathAsync(string path, SqlValue value,
            CancellationToken cancellationToken = default)
        {
            HashSet<RowId> rows = [];
            foreach (var key in CatalogIndexKey.EncodeJsonPathValueKeys(definition, path, value))
                rows.UnionWith(await tree.FindAsync(key, cancellationToken).ConfigureAwait(false));
            return rows.OrderBy(row => row.PageId.Value).ThenBy(row => row.SlotId.Value)
                .ThenBy(row => row.Generation).ToArray();
        }

        public async ValueTask<IReadOnlyList<RowId>> FindJsonPathExistsAsync(string path,
            CancellationToken cancellationToken = default)
        {
            HashSet<RowId> rows = [];
            foreach (var key in CatalogIndexKey.EncodeJsonPathExistsKeys(definition, path))
                rows.UnionWith(await tree.FindAsync(key, cancellationToken).ConfigureAwait(false));
            return rows.OrderBy(row => row.PageId.Value).ThenBy(row => row.SlotId.Value)
                .ThenBy(row => row.Generation).ToArray();
        }

        public ValueTask<IReadOnlyList<RowId>> FindXmlPathAsync(string path, string? value = null,
            CancellationToken cancellationToken = default) => tree.FindAsync(value is null
            ? CatalogIndexKey.EncodeXmlPathExists(definition, path)
            : CatalogIndexKey.EncodeXmlPathValue(definition, path, value), cancellationToken);
    }

    private async ValueTask FlushAndPublishAsync(CancellationToken cancellationToken)
    {
        await _bufferPool.FlushAllAsync(cancellationToken).ConfigureAwait(false);
        if (_catalog.RootPageId is { } root && _database.Header.CatalogRootPageId != root)
            await _database.PublishCatalogRootAsync(root, cancellationToken).ConfigureAwait(false);
    }
}
