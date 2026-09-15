using sql_storage_engine.Buffers;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Overflow;
using sql_storage_engine.Pages;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Tables;
using sql_storage_engine.Transactions;

namespace sql_storage_engine;

/// <summary>
/// Owns a database file and exposes logical catalog and row operations. Consumers should depend on
/// <see cref="IStorageEngine"/>, <see cref="IStorageCatalog"/>, and <see cref="IStorageTable"/>.
/// </summary>
public sealed partial class StorageEngine : IStorageEngine, IStorageCatalog
{
    private readonly PageDatabase _database;
    private readonly BufferPool _bufferPool;
    private CatalogService _catalog;
    private readonly OverflowManager _overflow;
    private readonly int _inlineValueThreshold;
    private readonly TimeProvider _timeProvider;
    private readonly StorageGate _accessGate = new();
    private long _handleGeneration;
    private bool _disposed;
    private IDisposable? _fileLease;

    private StorageEngine(PageDatabase database, BufferPool bufferPool, CatalogService catalog,
        StorageEngineOptions options)
    {
        _database = database;
        _bufferPool = bufferPool;
        _catalog = catalog;
        _overflow = new OverflowManager(bufferPool, database);
        _inlineValueThreshold = options.InlineValueThreshold;
        _timeProvider = options.TimeProvider;
    }

    public IStorageCatalog Catalog => this;
    public DatabaseId DatabaseId => _database.Header.DatabaseId;
    public IReadOnlyList<CatalogTable> Tables => ReadCatalog(() => _catalog.Tables.ToArray());
    public IReadOnlyList<CatalogIndex> Indexes => ReadCatalog(() => _catalog.Indexes.ToArray());
    public IReadOnlyList<CatalogScalarType> ScalarTypes => ReadCatalog(() => _catalog.ScalarTypes.ToArray());
    public IReadOnlyList<CatalogTableType> TableTypes => ReadCatalog(() => _catalog.TableTypes.ToArray());
    public IReadOnlyList<SqlXmlSchemaCollection> XmlSchemaCollections => ReadCatalog(() => _catalog.XmlSchemaCollections.ToArray());
    public IReadOnlyList<CatalogAssembly> Assemblies => ReadCatalog(() => _catalog.Assemblies.ToArray());
    public string DefaultCollation => _database.Header.DefaultCollation;

    private T ReadCatalog<T>(Func<T> read)
    {
        using var lease = _accessGate.EnterImmediate(); using var scope = lease.Activate();
        ThrowIfDisposed(); return read();
    }

    public static async ValueTask<StorageEngine> CreateAsync(string path, StorageEngineOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new StorageEngineOptions();
        Validate(options);
        var fileLease = StorageEngineFileLease.Acquire(path, creating: true);
        try
        {
            var database = await PageDatabase.CreateAsync(path, options.PageSize, options.DefaultCollation, cancellationToken).ConfigureAwait(false);
            try
            {
                var pool = new BufferPool(database, options.BufferPoolCapacity, leaveOpen: true);
                return new StorageEngine(database, pool, CatalogService.CreateEmpty(database, database, pool), options) { _fileLease = fileLease };
            }
            catch { await database.DisposeAsync().ConfigureAwait(false); throw; }
        }
        catch { fileLease.Dispose(); throw; }
    }

    public static async ValueTask<StorageEngine> OpenAsync(string path, StorageEngineOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new StorageEngineOptions();
        Validate(options);
        var fileLease = StorageEngineFileLease.Acquire(path, creating: false);
        try
        {
            await StorageTransactionFiles.RecoverAsync(Path.GetFullPath(path), cancellationToken).ConfigureAwait(false);
            var database = await PageDatabase.OpenAsync(path, cancellationToken).ConfigureAwait(false);
            BufferPool? pool = null;
            try
            {
                pool = new BufferPool(database, options.BufferPoolCapacity, leaveOpen: true);
                var catalog = database.Header.CatalogRootPageId is { } root
                    ? await CatalogService.OpenAsync(root, database, database, pool, cancellationToken).ConfigureAwait(false)
                    : CatalogService.CreateEmpty(database, database, pool);
                return new StorageEngine(database, pool, catalog, options) { _fileLease = fileLease };
            }
            catch
            {
                if (pool is not null) await pool.DisposeAsync().ConfigureAwait(false);
                await database.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
        catch { fileLease.Dispose(); throw; }
    }

    public bool TryGetTable(string name, out CatalogTable? table)
    {
        using var lease = _accessGate.EnterImmediate(); using var scope = lease.Activate();
        ThrowIfDisposed();
        return _catalog.TryOpenTable(name, out table);
    }
    public bool TryGetTable(CatalogTableName name, out CatalogTable? table)
    {
        using var lease = _accessGate.EnterImmediate(); using var scope = lease.Activate();
        ThrowIfDisposed();
        return _catalog.TryOpenTable(name, out table);
    }
    public bool TryGetTable(TableId id, out CatalogTable? table)
    {
        using var lease = _accessGate.EnterImmediate(); using var scope = lease.Activate();
        ThrowIfDisposed();
        return _catalog.TryOpenTable(id, out table);
    }
    public bool TryGetTemporalHistory(TableId currentTableId, out CatalogTable? historyTable)
    {
        using var lease = _accessGate.EnterImmediate(); using var scope = lease.Activate();
        ThrowIfDisposed();
        historyTable = _catalog.TryOpenTable(currentTableId, out var current) &&
                       current!.SystemVersioning is { } temporal &&
                       _catalog.TryOpenTable(temporal.HistoryTableId, out var history)
            ? history
            : null;
        return historyTable is not null;
    }
    public bool TryGetTemporalCurrent(TableId historyTableId, out CatalogTable? currentTable)
    {
        using var lease = _accessGate.EnterImmediate(); using var scope = lease.Activate();
        ThrowIfDisposed();
        currentTable = _catalog.Tables.SingleOrDefault(table =>
            table.SystemVersioning?.HistoryTableId == historyTableId);
        return currentTable is not null;
    }
    public bool TryGetIndex(TableId tableId, string name, out CatalogIndex? index)
    {
        using var lease = _accessGate.EnterImmediate(); using var scope = lease.Activate();
        ThrowIfDisposed();
        return _catalog.TryOpenIndex(name, tableId, out index);
    }
    public IReadOnlyList<CatalogIndex> GetIndexes(TableId tableId)
    {
        using var lease = _accessGate.EnterImmediate(); using var scope = lease.Activate();
        ThrowIfDisposed();
        return _catalog.Indexes.Where(index => index.TableId == tableId).ToArray();
    }
    public bool TryGetScalarType(string schemaName, string name, out CatalogScalarType? type)
    {
        using var lease = _accessGate.EnterImmediate(); using var scope = lease.Activate();
        ThrowIfDisposed();
        return _catalog.TryOpenScalarType(schemaName, name, out type);
    }
    public bool TryGetTableType(string schemaName, string name, out CatalogTableType? type)
    {
        using var lease = _accessGate.EnterImmediate(); using var scope = lease.Activate();
        ThrowIfDisposed();
        return _catalog.TryOpenTableType(schemaName, name, out type);
    }
    public bool TryGetXmlSchemaCollection(string schemaName, string name, out SqlXmlSchemaCollection? collection)
    {
        using var lease = _accessGate.EnterImmediate(); using var scope = lease.Activate();
        ThrowIfDisposed();
        return _catalog.TryOpenXmlSchemaCollection(schemaName, name, out collection);
    }
    public bool TryGetAssembly(string name, out CatalogAssembly? assembly)
    {
        using var lease = _accessGate.EnterImmediate(); using var scope = lease.Activate();
        ThrowIfDisposed();
        return _catalog.TryOpenAssembly(name, out assembly);
    }

    public async ValueTask<CatalogTable> CreateTableAsync(string name, IEnumerable<CatalogColumn> columns,
        CancellationToken cancellationToken = default)
        => await CreateTableAsync(name, columns, [], cancellationToken).ConfigureAwait(false);

    public async ValueTask<CatalogTable> CreateTableAsync(string name, IEnumerable<CatalogColumn> columns,
        IEnumerable<CatalogCheckConstraint> checkConstraints, CancellationToken cancellationToken = default)
    {
        using var accessLease = await _accessGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = accessLease.Activate();
        ThrowIfDisposed();
        var table = await _catalog.CreateTableAsync(name, 1, ApplyDatabaseCollation(columns), checkConstraints, cancellationToken).ConfigureAwait(false);
        await PublishCatalogAsync(cancellationToken).ConfigureAwait(false);
        return table;
    }

    public async ValueTask<CatalogTable> CreateTableAsync(CatalogTableName name,
        IEnumerable<CatalogColumn> columns, IEnumerable<CatalogCheckConstraint>? checkConstraints = null,
        CancellationToken cancellationToken = default)
    {
        using var accessLease = await _accessGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = accessLease.Activate();
        ThrowIfDisposed();
        var table = await _catalog.CreateTableAsync(name, 1, ApplyDatabaseCollation(columns), checkConstraints,
            cancellationToken).ConfigureAwait(false);
        await PublishCatalogAsync(cancellationToken).ConfigureAwait(false);
        return table;
    }

    public async ValueTask<CatalogTable> CreateSystemVersionedTableAsync(CatalogTableName name,
        IEnumerable<CatalogColumn> columns, ColumnId periodStartColumnId, ColumnId periodEndColumnId,
        CatalogTableName? historyTableName = null,
        IEnumerable<CatalogCheckConstraint>? checkConstraints = null,
        CancellationToken cancellationToken = default)
    {
        using var accessLease = await _accessGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = accessLease.Activate();
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(name);
        var columnSnapshot = ApplyDatabaseCollation(columns).ToArray();
        historyTableName ??= new CatalogTableName(name.DatabaseName, name.SchemaName, name.Name + "History");
        var table = await _catalog.CreateSystemVersionedTableAsync(name, historyTableName, 1, columnSnapshot,
            periodStartColumnId, periodEndColumnId, checkConstraints, cancellationToken).ConfigureAwait(false);
        await PublishCatalogAsync(cancellationToken).ConfigureAwait(false);
        return table;
    }

    public async ValueTask<CatalogIndex> CreateIndexAsync(string name, TableId tableId, bool isUnique,
        IEnumerable<CatalogIndexedColumn> columns, CancellationToken cancellationToken = default)
    {
        using var accessLease = await _accessGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = accessLease.Activate();
        ThrowIfDisposed();
        var index = await _catalog.CreateIndexAsync(name, tableId, isUnique, columns, cancellationToken).ConfigureAwait(false);
        await PublishCatalogAsync(cancellationToken).ConfigureAwait(false);
        return index;
    }

    public async ValueTask<CatalogIndex> CreateIndexAsync(string name, TableId tableId, bool isUnique,
        IEnumerable<CatalogIndexedColumn> columns, CatalogBTreeIndexOptions options,
        CancellationToken cancellationToken = default)
    {
        using var accessLease = await _accessGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = accessLease.Activate();
        ThrowIfDisposed();
        var index = await _catalog.CreateIndexAsync(name, tableId, isUnique, columns, options, cancellationToken).ConfigureAwait(false);
        await PublishCatalogAsync(cancellationToken).ConfigureAwait(false); return index;
    }

    public async ValueTask<CatalogIndex> CreateSpecializedIndexAsync(string name, TableId tableId,
        CatalogIndexedColumn column, CatalogSpecializedIndexOptions options,
        CancellationToken cancellationToken = default)
    {
        using var accessLease = await _accessGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = accessLease.Activate();
        ThrowIfDisposed();
        var index = await _catalog.CreateSpecializedIndexAsync(name, tableId, column, options, cancellationToken).ConfigureAwait(false);
        await PublishCatalogAsync(cancellationToken).ConfigureAwait(false);
        return index;
    }

    public async ValueTask<CatalogTable> RegisterGraphNodeTableAsync(TableId tableId, ColumnId identityColumnId,
        IndexId identityIndexId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        using var accessLease = await _accessGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = accessLease.Activate();
        ThrowIfDisposed();
        await EnsureTableEmptyAsync(tableId, cancellationToken).ConfigureAwait(false);
        var table = await _catalog.RegisterGraphNodeTableAsync(tableId, identityColumnId, identityIndexId,
            cancellationToken).ConfigureAwait(false);
        await PublishCatalogAsync(cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _handleGeneration);
        return table;
    }

    public async ValueTask<CatalogTable> RegisterGraphEdgeTableAsync(TableId tableId, ColumnId identityColumnId,
        IndexId identityIndexId, TableId fromNodeTableId, ColumnId fromNodeColumnId, IndexId outgoingIndexId,
        TableId toNodeTableId, ColumnId toNodeColumnId, IndexId incomingIndexId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        using var accessLease = await _accessGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = accessLease.Activate();
        ThrowIfDisposed();
        await EnsureTableEmptyAsync(tableId, cancellationToken).ConfigureAwait(false);
        var table = await _catalog.RegisterGraphEdgeTableAsync(tableId, identityColumnId, identityIndexId,
            fromNodeTableId, fromNodeColumnId, outgoingIndexId, toNodeTableId, toNodeColumnId,
            incomingIndexId, cancellationToken).ConfigureAwait(false);
        await PublishCatalogAsync(cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _handleGeneration);
        return table;
    }

    private async ValueTask EnsureTableEmptyAsync(TableId tableId, CancellationToken cancellationToken)
    {
        var storage = await OpenTableStorageAsync(tableId, cancellationToken).ConfigureAwait(false);
        await using var rows = storage.ScanAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        if (await rows.MoveNextAsync().ConfigureAwait(false))
            throw new InvalidOperationException("A table must be empty before graph metadata is registered.");
    }

    public async ValueTask<CatalogScalarType> CreateScalarTypeAsync(SqlType definition,
        CancellationToken cancellationToken = default)
    {
        using var accessLease = await _accessGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = accessLease.Activate();
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
        using var accessLease = await _accessGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = accessLease.Activate();
        ThrowIfDisposed();
        var type = await _catalog.CreateTableTypeAsync(schemaName, name, ApplyDatabaseCollation(columns), indexes, checkConstraints,
            isMemoryOptimized, cancellationToken)
            .ConfigureAwait(false);
        await PublishCatalogAsync(cancellationToken).ConfigureAwait(false);
        return type;
    }

    public async ValueTask<SqlXmlSchemaCollection> CreateXmlSchemaCollectionAsync(SqlXmlSchemaCollection collection,
        CancellationToken cancellationToken = default)
    {
        using var accessLease = await _accessGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = accessLease.Activate(); var result = await _catalog.CreateXmlSchemaCollectionAsync(collection, cancellationToken).ConfigureAwait(false); await PublishCatalogAsync(cancellationToken).ConfigureAwait(false); return result;
    }

    public async ValueTask<SqlXmlSchemaCollection> AlterXmlSchemaCollectionAsync(string schemaName, string name,
        IEnumerable<string> additionalDefinitions, CancellationToken cancellationToken = default)
    {
        using var accessLease = await _accessGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = accessLease.Activate(); var result = await _catalog.AlterXmlSchemaCollectionAsync(schemaName, name, additionalDefinitions, cancellationToken).ConfigureAwait(false); await PublishCatalogAsync(cancellationToken).ConfigureAwait(false); return result;
    }

    public async ValueTask DropXmlSchemaCollectionAsync(string schemaName, string name,
        CancellationToken cancellationToken = default)
    {
        using var accessLease = await _accessGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = accessLease.Activate(); await _catalog.DropXmlSchemaCollectionAsync(schemaName, name, cancellationToken).ConfigureAwait(false); await PublishCatalogAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<CatalogAssembly> CreateAssemblyAsync(CatalogAssembly assembly,
        CancellationToken cancellationToken = default)
    {
        using var accessLease = await _accessGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = accessLease.Activate(); var result = await _catalog.CreateAssemblyAsync(assembly, cancellationToken).ConfigureAwait(false); await PublishCatalogAsync(cancellationToken).ConfigureAwait(false); return result;
    }

    public async ValueTask DropAssemblyAsync(string name, CancellationToken cancellationToken = default)
    {
        using var accessLease = await _accessGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = accessLease.Activate(); await _catalog.DropAssemblyAsync(name, cancellationToken).ConfigureAwait(false); await PublishCatalogAsync(cancellationToken).ConfigureAwait(false);
    }

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
        using var accessLease = await _accessGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = accessLease.Activate();
        ThrowIfDisposed();
        var storage = await OpenTableStorageAsync(tableId, cancellationToken).ConfigureAwait(false);
        var generation = Volatile.Read(ref _handleGeneration);
        return new StorageTable(storage, this, coordinate: true, flushMutations: true,
            () => generation == Volatile.Read(ref _handleGeneration));
    }

    internal async ValueTask<TableStorage> OpenTableStorageAsync(TableId tableId,
        CancellationToken cancellationToken)
    {
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
            AllocateGeneratedValueAsync,
            () => _catalog.Indexes.Where(index => index.TableId == tableId)
                .Select(index => new TableIndex(index, _catalog.OpenIndex(index))).ToArray());
        return storage;
    }

    internal ValueTask<SqlValue> AllocateGeneratedValueAsync(CatalogGeneratedAlwaysKind kind,
        CancellationToken cancellationToken)
    {
        if (kind == CatalogGeneratedAlwaysKind.RowStart)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            return ValueTask.FromResult<SqlValue>(SqlValue.DateTime(DateTime.SpecifyKind(now, DateTimeKind.Unspecified)));
        }
        if (kind == CatalogGeneratedAlwaysKind.RowEnd)
            return ValueTask.FromResult<SqlValue>(SqlValue.DateTime(DateTime.MaxValue));
        return _database.AllocateGeneratedValueAsync(kind, cancellationToken);
    }

    public ValueTask<IStorageIndex> OpenIndexAsync(IndexId indexId,
        CancellationToken cancellationToken = default)
    {
        using var accessLease = _accessGate.EnterImmediate();
        using var accessScope = accessLease.Activate();
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        var index = _catalog.Indexes.SingleOrDefault(candidate => candidate.Id == indexId)
            ?? throw new KeyNotFoundException($"Unknown index {indexId}.");
        var table = _catalog.Tables.Single(candidate => candidate.Id == index.TableId);
        return ValueTask.FromResult<IStorageIndex>(new StorageIndex(index, table, _catalog.OpenIndex(index), this));
    }

    public async ValueTask<IStorageGraphNodeTable> OpenGraphNodeTableAsync(TableId tableId,
        CancellationToken cancellationToken = default)
    {
        using var accessLease = await _accessGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = accessLease.Activate();
        ThrowIfDisposed();
        var storage = await OpenTableStorageAsync(tableId, cancellationToken).ConfigureAwait(false);
        if (storage.Definition.Graph?.Kind != GraphTableKind.Node)
            throw new InvalidOperationException("The requested table is not a registered graph node table.");
        return new StorageGraphNodeTable(storage, this, Volatile.Read(ref _handleGeneration));
    }

    public async ValueTask<IStorageGraphEdgeTable> OpenGraphEdgeTableAsync(TableId tableId,
        CancellationToken cancellationToken = default)
    {
        using var accessLease = await _accessGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = accessLease.Activate();
        ThrowIfDisposed();
        var storage = await OpenTableStorageAsync(tableId, cancellationToken).ConfigureAwait(false);
        if (storage.Definition.Graph?.Kind != GraphTableKind.Edge)
            throw new InvalidOperationException("The requested table is not a registered graph edge table.");
        return new StorageGraphEdgeTable(storage, this, Volatile.Read(ref _handleGeneration));
    }

    internal ValueTask<IStorageIndex> OpenScopedIndexAsync(IndexId indexId, Func<bool> active, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var index = _catalog.Indexes.Single(candidate => candidate.Id == indexId);
        var table = _catalog.Tables.Single(candidate => candidate.Id == index.TableId);
        return ValueTask.FromResult<IStorageIndex>(new StorageIndex(index, table, _catalog.OpenIndex(index), this, active));
    }

    internal CatalogService GraphCatalog => _catalog;
    internal Action<GraphDdlStage>? GraphDdlObserver { get; set; }
    internal void ObserveGraphDdl(GraphDdlStage stage, CancellationToken token)
    {
        GraphDdlObserver?.Invoke(stage);
        token.ThrowIfCancellationRequested();
    }
    internal void EnsureGraphHandleCurrent(long generation)
    {
        ThrowIfDisposed();
        if (generation != Volatile.Read(ref _handleGeneration))
            throw new InvalidOperationException("The graph handle is stale after catalog replacement or rollback; reopen it.");
    }

    public async ValueTask<StorageTableStatistics> GetTableStatisticsAsync(TableId tableId,
        CancellationToken cancellationToken = default)
    {
        using var accessLease = await _accessGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = accessLease.Activate();
        ThrowIfDisposed();
        if (!_catalog.TryOpenTable(tableId, out var definition))
            throw new KeyNotFoundException($"Unknown table {tableId}.");
        var heap = await _catalog.OpenHeapAsync(definition!, cancellationToken).ConfigureAwait(false);
        var storage = await OpenTableStorageAsync(tableId, cancellationToken).ConfigureAwait(false);
        var nullCounts = new long[definition!.Columns.Count];
        var distinct = definition.Columns.Select(_ => new HashSet<SqlValue>()).ToArray();
        long rowCount = 0;
        await foreach (var stored in storage.ScanAsync(cancellationToken).ConfigureAwait(false))
        {
            rowCount++;
            for (var index = 0; index < stored.Row.Values.Count; index++)
            {
                var value = stored.Row.Values[index];
                if (value.IsNull) nullCounts[index]++;
                else distinct[index].Add(value);
            }
        }
        var pages = await heap.GetPageIdsAsync(int.MaxValue, cancellationToken).ConfigureAwait(false);
        return new StorageTableStatistics(tableId, rowCount, pages.Count,
            definition.Columns.Select((column, index) => new StorageColumnStatistics(
                column.Id, nullCounts[index], distinct[index].Count)).ToArray(), DateTimeOffset.UtcNow);
    }

    public async ValueTask<StorageIndexStatistics> GetIndexStatisticsAsync(IndexId indexId,
        CancellationToken cancellationToken = default)
    {
        using var accessLease = await _accessGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = accessLease.Activate();
        ThrowIfDisposed();
        var definition = _catalog.Indexes.SingleOrDefault(candidate => candidate.Id == indexId)
            ?? throw new KeyNotFoundException($"Unknown index {indexId}.");
        var tree = _catalog.OpenIndex(definition);
        long entryCount = 0;
        long distinctKeyCount = 0;
        IndexKey? previous = null;
        await foreach (var entry in tree.ScanAsync(new IndexRange(null, null), cancellationToken).ConfigureAwait(false))
        {
            entryCount++;
            if (previous is null || !previous.Equals(entry.Key))
            {
                distinctKeyCount++;
                previous = entry.Key;
            }
        }
        var pages = await tree.GetLeafPageIdsAsync(cancellationToken).ConfigureAwait(false);
        var histogram = definition.Method == CatalogIndexMethod.BTree
            ? await BuildHistogramAsync(definition, cancellationToken).ConfigureAwait(false)
            : [];
        return new StorageIndexStatistics(indexId, entryCount, distinctKeyCount, pages.Count, histogram,
            DateTimeOffset.UtcNow);
    }

    private async ValueTask<IReadOnlyList<StorageHistogramBucket>> BuildHistogramAsync(CatalogIndex index,
        CancellationToken cancellationToken)
    {
        const int maximumBuckets = 200;
        var table = _catalog.Tables.Single(candidate => candidate.Id == index.TableId);
        var storage = await OpenTableStorageAsync(table.Id, cancellationToken).ConfigureAwait(false);
        var groups = new Dictionary<IndexKey, (SqlValue[] Values, long Count)>();
        await foreach (var stored in storage.ScanAsync(cancellationToken).ConfigureAwait(false))
        {
            var values = index.Columns.Select(indexed => table.Columns.Select((column, position) => (column, position))
                .Single(item => item.column.Id == indexed.ColumnId).position)
                .Select(position => stored.Row.Values[position]).ToArray();
            var key = CatalogIndexKey.EncodeValues(values, table, index);
            groups[key] = groups.TryGetValue(key, out var existing)
                ? (existing.Values, existing.Count + 1)
                : (values, 1);
        }
        if (groups.Count == 0) return [];
        var ordered = groups.OrderBy(group => group.Key).Select(group => group.Value).ToArray();
        var bucketCount = Math.Min(maximumBuckets, ordered.Length);
        List<StorageHistogramBucket> histogram = new(bucketCount);
        for (var bucket = 0; bucket < bucketCount; bucket++)
        {
            var start = bucket * ordered.Length / bucketCount;
            var end = (bucket + 1) * ordered.Length / bucketCount;
            var upper = ordered[end - 1];
            histogram.Add(new StorageHistogramBucket(upper.Values,
                ordered[start..(end - 1)].Sum(group => group.Count), upper.Count, end - start - 1));
        }
        return histogram.AsReadOnly();
    }

    public async ValueTask ExecuteStatementAsync(Func<IStorageStatement, CancellationToken, ValueTask> operation,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(operation);
        using var accessLease = await _accessGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = accessLease.Activate();
        ThrowIfDisposed();
        StatementJournal? journal = null;
        var statement = new StorageStatement(this);
        try
        {
            await FlushAndPublishAsync(cancellationToken).ConfigureAwait(false);
            journal = await StatementJournal.CreateAsync(_database.DatabasePath, cancellationToken).ConfigureAwait(false);
            try
            {
                await operation(statement, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                statement.Complete();
                await accessScope.DrainAsync().ConfigureAwait(false);
            }
            statement.ThrowIfDdlFailed();
            cancellationToken.ThrowIfCancellationRequested();
            statement.Complete();
            await FlushAndPublishAsync(cancellationToken).ConfigureAwait(false);
            await journal.MarkCommittedAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception failure)
        {
            statement.Complete();
            if (journal is not null)
            {
                try
                {
                    await _bufferPool.DiscardAllAsync(CancellationToken.None).ConfigureAwait(false);
                    await journal.RestoreAsync(_database, CancellationToken.None).ConfigureAwait(false);
                    _catalog = _database.Header.CatalogRootPageId is { } root
                        ? await CatalogService.OpenAsync(root, _database, _database, _bufferPool,
                            CancellationToken.None).ConfigureAwait(false)
                        : CatalogService.CreateEmpty(_database, _database, _bufferPool);
                    Interlocked.Increment(ref _handleGeneration);
                }
                catch (Exception rollbackFailure)
                {
                    throw new AggregateException("Statement failed and its durable rollback also failed.",
                        failure, rollbackFailure);
                }
            }
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            throw;
        }
        finally
        {
            if (journal is not null) await journal.DisposeAsync().ConfigureAwait(false);

        }
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        using var accessLease = await _accessGate.EnterAsync(cancellationToken).ConfigureAwait(false);
        using var accessScope = accessLease.Activate();
        ThrowIfDisposed();
        await _bufferPool.FlushAllAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        if (IsInStorageScope) throw new InvalidOperationException("Dispose storage after its callback returns.");
        if (PendingTransactionCleanup is { IsCompleted: false })
            throw new InvalidOperationException("Transaction cleanup is still running; retry disposal after the callback stops.");
        if (_activeTransaction is { } transaction)
        {
            try { await transaction.DisposeAsync().ConfigureAwait(false); }
            catch { _quarantined = true; }
        }
        if (PendingTransactionCleanup is { IsCompleted: false })
            throw new InvalidOperationException("Transaction cleanup is still running; retry disposal after the callback stops.");
        using var lease = await _accessGate.EnterAsync(CancellationToken.None, reentrant: false).ConfigureAwait(false);
        if (_disposed) return;
        await _securityEventsGate.WaitAsync().ConfigureAwait(false);
        _disposed = true;
        try
        {
            if (_quarantined) await _bufferPool.DiscardAllAsync(CancellationToken.None).ConfigureAwait(false);
            await _bufferPool.DisposeAsync().ConfigureAwait(false);
            await _database.DisposeAsync().ConfigureAwait(false);
        }
        finally { _fileLease?.Dispose(); _securityEventsGate.Release(); }
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
        ArgumentNullException.ThrowIfNull(options.TimeProvider);
        _ = SqlCollation.Parse(options.DefaultCollation);
    }

    internal void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_quarantined) throw new InvalidOperationException("Database access is quarantined pending transaction recovery.");
    }
    internal bool IsInStorageScope => _accessGate.Current is not null;



    internal async ValueTask<IDisposable> EnterStatementGateAsync(CancellationToken cancellationToken, bool reentrant = false)
    {
        ThrowIfDisposed();
        var lease = await _accessGate.EnterAsync(cancellationToken, reentrant).ConfigureAwait(false);
        try { ThrowIfDisposed(); return lease; }
        catch { lease.Dispose(); throw; }
    }

    internal IDisposable ActivateGate(IDisposable lease) => ((StorageGateLease)lease).Activate();

    internal long HandleGeneration => Volatile.Read(ref _handleGeneration);

    internal IStorageTable CreateStatementTable(TableStorage storage, Func<bool> isActive) =>
        new StorageTable(storage, this, coordinate: false, flushMutations: false, isActive);



    internal async ValueTask FlushAndPublishAsync(CancellationToken cancellationToken)
    {
        await _bufferPool.FlushAllAsync(cancellationToken).ConfigureAwait(false);
        if (_catalog.RootPageId is { } root && _database.Header.CatalogRootPageId != root)
            await _database.PublishCatalogRootAsync(root, cancellationToken).ConfigureAwait(false);
    }
}
