using sql_storage_engine.Buffers;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Overflow;
using sql_storage_engine.Pages;
using sql_storage_engine.Rows;
using sql_storage_engine.Tables;
using sql_storage_engine.Transactions;

namespace sql_storage_engine;

/// <summary>
/// Owns a database file and exposes logical catalog and row operations. Consumers should depend on
/// <see cref="IStorageEngine"/>, <see cref="IStorageCatalog"/>, and <see cref="IStorageTable"/>.
/// </summary>
public sealed class StorageEngine : IStorageEngine, IStorageCatalog
{
    private readonly PageDatabase _database;
    private readonly BufferPool _bufferPool;
    private CatalogService _catalog;
    private readonly OverflowManager _overflow;
    private readonly int _inlineValueThreshold;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _statementGate = new(1, 1);
    private long _handleGeneration;
    private bool _disposed;

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
    public bool TryGetTable(CatalogTableName name, out CatalogTable? table) => _catalog.TryOpenTable(name, out table);
    public bool TryGetTable(TableId id, out CatalogTable? table) => _catalog.TryOpenTable(id, out table);
    public bool TryGetTemporalHistory(TableId currentTableId, out CatalogTable? historyTable)
    {
        historyTable = _catalog.TryOpenTable(currentTableId, out var current) &&
                       current!.SystemVersioning is { } temporal &&
                       _catalog.TryOpenTable(temporal.HistoryTableId, out var history)
            ? history
            : null;
        return historyTable is not null;
    }
    public bool TryGetTemporalCurrent(TableId historyTableId, out CatalogTable? currentTable)
    {
        currentTable = _catalog.Tables.SingleOrDefault(table =>
            table.SystemVersioning?.HistoryTableId == historyTableId);
        return currentTable is not null;
    }
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

    public async ValueTask<CatalogTable> CreateTableAsync(CatalogTableName name,
        IEnumerable<CatalogColumn> columns, IEnumerable<CatalogCheckConstraint>? checkConstraints = null,
        CancellationToken cancellationToken = default)
    {
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
        var storage = await OpenTableStorageAsync(tableId, cancellationToken).ConfigureAwait(false);
        var generation = Volatile.Read(ref _handleGeneration);
        return new StorageTable(storage, this, coordinate: true, flushMutations: true,
            () => generation == Volatile.Read(ref _handleGeneration));
    }

    private async ValueTask<TableStorage> OpenTableStorageAsync(TableId tableId,
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

    private ValueTask<SqlValue> AllocateGeneratedValueAsync(CatalogGeneratedAlwaysKind kind,
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
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        var index = _catalog.Indexes.SingleOrDefault(candidate => candidate.Id == indexId)
            ?? throw new KeyNotFoundException($"Unknown index {indexId}.");
        var table = _catalog.Tables.Single(candidate => candidate.Id == index.TableId);
        return ValueTask.FromResult<IStorageIndex>(new StorageIndex(index, table, _catalog.OpenIndex(index), this));
    }

    public async ValueTask<StorageTableStatistics> GetTableStatisticsAsync(TableId tableId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        using var lease = await EnterStatementGateAsync(cancellationToken).ConfigureAwait(false);
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
        ThrowIfDisposed();
        using var lease = await EnterStatementGateAsync(cancellationToken).ConfigureAwait(false);
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
        await _statementGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        StatementJournal? journal = null;
        var statement = new StorageStatement(this);
        try
        {
            await FlushAndPublishAsync(cancellationToken).ConfigureAwait(false);
            journal = await StatementJournal.CreateAsync(_database.DatabasePath, cancellationToken).ConfigureAwait(false);
            await operation(statement, cancellationToken).ConfigureAwait(false);
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
            _statementGate.Release();
        }
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
        ArgumentNullException.ThrowIfNull(options.TimeProvider);
        _ = SqlCollation.Parse(options.DefaultCollation);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed class StorageTable(TableStorage storage, StorageEngine owner, bool coordinate,
        bool flushMutations, Func<bool> isActive) : IStorageTable
    {
        public CatalogTable Definition => storage.Definition;
        public async ValueTask<RowId> InsertAsync(Row row, CancellationToken cancellationToken = default)
        {
            EnsureActive(); using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false);
            RejectHistoryMutation();
            var id = await storage.InsertAsync(row, cancellationToken).ConfigureAwait(false);
            if (flushMutations) await owner.FlushAndPublishAsync(cancellationToken).ConfigureAwait(false); return id;
        }
        public async ValueTask<TableInsertResult> TryInsertAsync(Row row, CancellationToken cancellationToken = default)
        {
            EnsureActive(); using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false);
            RejectHistoryMutation();
            var result = await storage.TryInsertAsync(row, cancellationToken).ConfigureAwait(false);
            if (flushMutations) await owner.FlushAndPublishAsync(cancellationToken).ConfigureAwait(false); return result;
        }
        public async ValueTask<StoredRow?> GetAsync(RowId rowId, CancellationToken cancellationToken = default)
        {
            EnsureActive(); using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false);
            var result = await storage.TryGetAsync(rowId, cancellationToken).ConfigureAwait(false);
            return result.Found ? new StoredRow(rowId, result.Row!) : null;
        }
        public async ValueTask<StoredRow?> GetAsync(RowId rowId, StorageReadOptions readOptions,
            CancellationToken cancellationToken = default)
        {
            EnsureActive(); using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(readOptions);
            var result = await storage.TryGetAsync(rowId, cancellationToken).ConfigureAwait(false);
            return result.Found ? new StoredRow(rowId, ApplyMasking(result.Row!, readOptions)) : null;
        }
        public async IAsyncEnumerable<StoredRow> ScanAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            EnsureActive(); using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false);
            await foreach (var stored in storage.ScanAsync(cancellationToken).ConfigureAwait(false)) yield return stored;
        }
        public async IAsyncEnumerable<StoredRow> ScanAsync(StorageReadOptions readOptions,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            EnsureActive(); using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(readOptions);
            await foreach (var stored in storage.ScanAsync(cancellationToken).ConfigureAwait(false))
                yield return stored with { Row = ApplyMasking(stored.Row, readOptions) };
        }
        public async IAsyncEnumerable<StoredRow> SampleAsync(StorageTableSample sample,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            EnsureActive(); using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(sample);
            await foreach (var stored in storage.SampleAsync(sample, cancellationToken).ConfigureAwait(false))
                yield return stored;
        }
        public async IAsyncEnumerable<StoredRow> SampleAsync(StorageTableSample sample,
            StorageReadOptions readOptions,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            EnsureActive(); using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(sample);
            ArgumentNullException.ThrowIfNull(readOptions);
            await foreach (var stored in storage.SampleAsync(sample, cancellationToken).ConfigureAwait(false))
                yield return stored with { Row = ApplyMasking(stored.Row, readOptions) };
        }
        public async IAsyncEnumerable<StorageTemporalRow> TemporalScanAsync(StorageTemporalQuery query,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            EnsureActive(); using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(query);
            await foreach (var stored in TemporalRowsAsync(query, cancellationToken).ConfigureAwait(false))
                yield return stored;
        }
        public async IAsyncEnumerable<StorageTemporalRow> TemporalScanAsync(StorageTemporalQuery query,
            StorageReadOptions readOptions,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            EnsureActive(); using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(readOptions);
            await foreach (var stored in TemporalRowsAsync(query, cancellationToken).ConfigureAwait(false))
                yield return stored with { Row = ApplyMasking(stored.Row, readOptions) };
        }
        public async ValueTask<TableUpdateResult> UpdateAsync(RowId rowId, RowUpdate update, CancellationToken cancellationToken = default)
        {
            EnsureActive();
            if (coordinate && Definition.SystemVersioning is not null)
            {
                TableUpdateResult? atomicResult = null;
                await owner.ExecuteStatementAsync(async (statement, token) =>
                {
                    var table = await statement.OpenTableAsync(Definition.Id, token).ConfigureAwait(false);
                    atomicResult = await table.UpdateAsync(rowId, update, token).ConfigureAwait(false);
                }, cancellationToken).ConfigureAwait(false);
                return atomicResult!;
            }
            using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false);
            RejectHistoryMutation();
            var result = Definition.SystemVersioning is null
                ? await storage.UpdateAsync(rowId, update, cancellationToken).ConfigureAwait(false)
                : await UpdateTemporalAsync(rowId, update, cancellationToken).ConfigureAwait(false);
            if (flushMutations) await owner.FlushAndPublishAsync(cancellationToken).ConfigureAwait(false); return result;
        }
        public async ValueTask<TableDeleteResult> DeleteAsync(RowId rowId, CancellationToken cancellationToken = default)
        {
            EnsureActive();
            if (coordinate && Definition.SystemVersioning is not null)
            {
                TableDeleteResult? atomicResult = null;
                await owner.ExecuteStatementAsync(async (statement, token) =>
                {
                    var table = await statement.OpenTableAsync(Definition.Id, token).ConfigureAwait(false);
                    atomicResult = await table.DeleteAsync(rowId, token).ConfigureAwait(false);
                }, cancellationToken).ConfigureAwait(false);
                return atomicResult!;
            }
            using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false);
            RejectHistoryMutation();
            var result = Definition.SystemVersioning is null
                ? await storage.DeleteAsync(rowId, cancellationToken).ConfigureAwait(false)
                : await DeleteTemporalAsync(rowId, cancellationToken).ConfigureAwait(false);
            if (flushMutations) await owner.FlushAndPublishAsync(cancellationToken).ConfigureAwait(false); return result;
        }

        private async IAsyncEnumerable<StorageTemporalRow> TemporalRowsAsync(StorageTemporalQuery query,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var temporal = Definition.SystemVersioning ?? throw new InvalidOperationException(
                $"Table {Definition.QualifiedName} is not system-versioned.");
            var history = await owner.OpenTableStorageAsync(temporal.HistoryTableId, cancellationToken)
                .ConfigureAwait(false);
            await foreach (var row in history.ScanAsync(cancellationToken).ConfigureAwait(false))
                if (MatchesTemporal(row.Row, temporal, query))
                    yield return new StorageTemporalRow(temporal.HistoryTableId, row.RowId, row.Row);
            await foreach (var row in storage.ScanAsync(cancellationToken).ConfigureAwait(false))
                if (MatchesTemporal(row.Row, temporal, query))
                    yield return new StorageTemporalRow(Definition.Id, row.RowId, row.Row);
        }

        private async ValueTask<TableUpdateResult> UpdateTemporalAsync(RowId rowId, RowUpdate update,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(update);
            var old = await storage.TryGetAsync(rowId, cancellationToken).ConfigureAwait(false);
            if (!old.Found) return new TableUpdateResult(false, rowId, rowId);
            var (history, archived, timestamp) = await ArchiveAsync(old.Row!, cancellationToken).ConfigureAwait(false);
            try
            {
                var result = await storage.UpdateSystemVersionedAsync(rowId, update, timestamp, cancellationToken)
                    .ConfigureAwait(false);
                if (!result.Updated) await history.DeleteAsync(archived, CancellationToken.None).ConfigureAwait(false);
                return result;
            }
            catch
            {
                await history.DeleteAsync(archived, CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }

        private async ValueTask<TableDeleteResult> DeleteTemporalAsync(RowId rowId,
            CancellationToken cancellationToken)
        {
            var old = await storage.TryGetAsync(rowId, cancellationToken).ConfigureAwait(false);
            if (!old.Found) return new TableDeleteResult(false, []);
            var (history, archived, _) = await ArchiveAsync(old.Row!, cancellationToken).ConfigureAwait(false);
            try
            {
                var result = await storage.DeleteAsync(rowId, cancellationToken).ConfigureAwait(false);
                if (!result.Deleted) await history.DeleteAsync(archived, CancellationToken.None).ConfigureAwait(false);
                return result;
            }
            catch
            {
                await history.DeleteAsync(archived, CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }

        private async ValueTask<(TableStorage History, RowId Archived, SqlValue Timestamp)> ArchiveAsync(Row oldRow,
            CancellationToken cancellationToken)
        {
            var temporal = Definition.SystemVersioning!;
            var history = await owner.OpenTableStorageAsync(temporal.HistoryTableId, cancellationToken)
                .ConfigureAwait(false);
            var endPosition = ColumnPosition(temporal.PeriodEndColumnId);
            var startColumn = Definition.Columns[ColumnPosition(temporal.PeriodStartColumnId)];
            var timestamp = SqlConversion.ConvertTo(startColumn.Type,
                await owner.AllocateGeneratedValueAsync(CatalogGeneratedAlwaysKind.RowStart, cancellationToken)
                    .ConfigureAwait(false));
            var values = oldRow.Values.ToArray();
            if (((DateTimeSqlValue)timestamp).Value <
                ((DateTimeSqlValue)values[ColumnPosition(temporal.PeriodStartColumnId)]).Value)
                throw new InvalidOperationException("The temporal clock moved before the current row's period start.");
            values[endPosition] = timestamp;
            var inserted = await history.InsertPreparedAsync(new Row(values), cancellationToken).ConfigureAwait(false);
            if (!inserted.Inserted) throw new InvalidOperationException("A temporal history insert was unexpectedly ignored.");
            return (history, inserted.RowId!.Value, timestamp);
        }

        private bool MatchesTemporal(Row row, CatalogSystemVersioning temporal, StorageTemporalQuery query)
        {
            var start = ((DateTimeSqlValue)row.Values[ColumnPosition(temporal.PeriodStartColumnId)]).Value;
            var end = ((DateTimeSqlValue)row.Values[ColumnPosition(temporal.PeriodEndColumnId)]).Value;
            return query switch
            {
                StorageTemporalAsOf asOf => start <= asOf.Instant && end > asOf.Instant,
                StorageTemporalFromTo range => start < range.End && end > range.Start,
                StorageTemporalBetweenAnd range => start <= range.End && end > range.Start,
                StorageTemporalContainedIn range => start >= range.Start && end <= range.End,
                StorageTemporalAll => true,
                _ => throw new ArgumentOutOfRangeException(nameof(query), query, "Unknown temporal query type.")
            };
        }

        private int ColumnPosition(ColumnId id) => Definition.Columns.Select((column, position) => (column, position))
            .Single(item => item.column.Id == id).position;

        private void RejectHistoryMutation()
        {
            if (owner.TryGetTemporalCurrent(Definition.Id, out var current))
                throw new InvalidOperationException(
                    $"Temporal history table {Definition.QualifiedName} is maintained by {current!.QualifiedName}.");
        }

        private void EnsureActive()
        {
            if (!isActive()) throw new InvalidOperationException("The statement scope has completed.");
        }
        private async ValueTask<IDisposable> EnterAsync(CancellationToken cancellationToken)
            => coordinate ? await owner.EnterStatementGateAsync(cancellationToken).ConfigureAwait(false) : NoOpLease.Instance;

        private Row ApplyMasking(Row row, StorageReadOptions readOptions) => new(row.Values.Select((value, position) =>
        {
            var column = Definition.Columns[position];
            return readOptions.CanUnmask(column.Id) ? value : SqlDataMasking.Apply(column, value);
        }));
    }

    private async ValueTask<IDisposable> EnterStatementGateAsync(CancellationToken cancellationToken)
    {
        await _statementGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new GateLease(_statementGate);
    }

    private sealed class GateLease(SemaphoreSlim gate) : IDisposable
    {
        private SemaphoreSlim? _gate = gate;
        public void Dispose() => Interlocked.Exchange(ref _gate, null)?.Release();
    }

    private sealed class NoOpLease : IDisposable
    {
        public static NoOpLease Instance { get; } = new();
        public void Dispose() { }
    }

    private sealed class StorageStatement(StorageEngine owner) : IStorageStatement
    {
        private bool _active = true;
        public async ValueTask<CatalogTable> CreateTableAsync(CatalogTableName name,
            IEnumerable<CatalogColumn> columns, IEnumerable<CatalogCheckConstraint>? checkConstraints = null,
            CancellationToken cancellationToken = default)
        {
            if (!_active) throw new InvalidOperationException("The statement scope has completed.");
            return await owner.CreateTableAsync(name, columns, checkConstraints, cancellationToken)
                .ConfigureAwait(false);
        }
        public async ValueTask<IStorageTable> OpenTableAsync(TableId tableId,
            CancellationToken cancellationToken = default)
        {
            if (!_active) throw new InvalidOperationException("The statement scope has completed.");
            var storage = await owner.OpenTableStorageAsync(tableId, cancellationToken).ConfigureAwait(false);
            return new StorageTable(storage, owner, coordinate: false, flushMutations: false, () => _active);
        }
        public void Complete() => _active = false;
    }

    private sealed class StorageIndex(CatalogIndex definition, CatalogTable table, PersistentBPlusTree tree,
        StorageEngine owner) : IStorageIndex
    {
        private readonly long _generation = Volatile.Read(ref owner._handleGeneration);
        public CatalogIndex Definition => definition;

        public async ValueTask<IReadOnlyList<RowId>> FindAsync(IReadOnlyList<SqlValue> values,
            CancellationToken cancellationToken = default)
        {
            EnsureCurrent(); using var lease = await owner.EnterStatementGateAsync(cancellationToken).ConfigureAwait(false);
            return await tree.FindAsync(CatalogIndexKey.EncodeValues(values, table, definition), cancellationToken)
                .ConfigureAwait(false);
        }

        public async ValueTask<IReadOnlyList<StorageIndexEntry>> FindEntriesAsync(IReadOnlyList<SqlValue> values,
            CancellationToken cancellationToken = default)
        {
            EnsureCurrent(); using var lease = await owner.EnterStatementGateAsync(cancellationToken).ConfigureAwait(false);
            return (await tree.FindEntriesAsync(CatalogIndexKey.EncodeValues(values, table, definition), cancellationToken)
                .ConfigureAwait(false)).Select(ToStorageEntry).ToArray();
        }

        public async IAsyncEnumerable<RowId> ScanAsync(IReadOnlyList<SqlValue> lowerBound,
            IReadOnlyList<SqlValue> upperBound, bool includeLowerBound = true, bool includeUpperBound = true,
            ScanDirection direction = ScanDirection.Ascending,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            EnsureCurrent(); using var lease = await owner.EnterStatementGateAsync(cancellationToken).ConfigureAwait(false);
            var range = new IndexRange(CatalogIndexKey.EncodeValues(lowerBound, table, definition),
                CatalogIndexKey.EncodeValues(upperBound, table, definition), includeLowerBound, includeUpperBound, direction);
            await foreach (var entry in tree.ScanAsync(range, cancellationToken).ConfigureAwait(false))
                yield return entry.RowId;
        }

        public async IAsyncEnumerable<RowId> ScanAsync(StorageIndexRange range,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            EnsureCurrent(); using var lease = await owner.EnterStatementGateAsync(cancellationToken).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(range);
            if (definition.Method != CatalogIndexMethod.BTree)
                throw new InvalidOperationException("Bounded scans require a B-tree index.");
            var lower = EncodeBound(range.LowerBound, lower: true);
            var upper = EncodeBound(range.UpperBound, lower: false);
            if (lower.BeyondEnd) yield break;
            var physical = new IndexRange(lower.Key, upper.Key, lower.Inclusive, upper.Inclusive, range.Direction);
            await foreach (var entry in tree.ScanAsync(physical, cancellationToken).ConfigureAwait(false))
                yield return entry.RowId;
        }

        public async IAsyncEnumerable<StorageIndexEntry> ScanEntriesAsync(StorageIndexRange range,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            EnsureCurrent(); using var lease = await owner.EnterStatementGateAsync(cancellationToken).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(range);
            var lower = EncodeBound(range.LowerBound, lower: true);
            var upper = EncodeBound(range.UpperBound, lower: false);
            if (lower.BeyondEnd) yield break;
            var physical = new IndexRange(lower.Key, upper.Key, lower.Inclusive, upper.Inclusive, range.Direction);
            await foreach (var entry in tree.ScanAsync(physical, cancellationToken).ConfigureAwait(false))
                yield return ToStorageEntry(entry);
        }

        private StorageIndexEntry ToStorageEntry(LeafIndexEntry entry) => new(entry.RowId,
            CatalogIndexKey.DecodeIncludedValues(entry.IncludedPayload.Span, table, definition));

        private (IndexKey? Key, bool Inclusive, bool BeyondEnd) EncodeBound(StorageIndexBound? bound, bool lower)
        {
            if (bound is null) return (null, true, false);
            ArgumentNullException.ThrowIfNull(bound.Values);
            if (bound.Values.Count == 0 || bound.Values.Count > definition.Columns.Count)
                throw new ArgumentException($"A bound must contain between 1 and {definition.Columns.Count} values.",
                    lower ? nameof(StorageIndexRange.LowerBound) : nameof(StorageIndexRange.UpperBound));
            var key = CatalogIndexKey.EncodePrefix(bound.Values, table, definition);
            if (bound.Values.Count == definition.Columns.Count) return (key, bound.Inclusive, false);

            // Every complete key sharing a composite prefix sorts in [prefix, successor(prefix)).
            if (lower && bound.Inclusive || !lower && !bound.Inclusive) return (key, false, false);
            var successor = PrefixSuccessor(key);
            return successor is null ? (null, false, lower) : (successor, false, false);
        }

        private static IndexKey? PrefixSuccessor(IndexKey prefix)
        {
            var bytes = prefix.Bytes.ToArray();
            for (var index = bytes.Length - 1; index >= 0; index--)
            {
                if (bytes[index] == byte.MaxValue) continue;
                bytes[index]++;
                return new IndexKey(bytes.AsSpan(0, index + 1));
            }
            return null;
        }

        public async ValueTask<IReadOnlyList<SpecializedIndexMatch>> SearchNearestAsync(SqlValue query, int count,
            CancellationToken cancellationToken = default)
        {
            EnsureCurrent(); using var lease = await owner.EnterStatementGateAsync(cancellationToken).ConfigureAwait(false);
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
            var storage = await owner.OpenTableStorageAsync(table.Id, cancellationToken).ConfigureAwait(false);
            List<SpecializedIndexMatch> matches = [];
            var range = new IndexRange(new IndexKey([0]), new IndexKey([2]));
            await foreach (var entry in tree.ScanAsync(range, cancellationToken).ConfigureAwait(false))
            {
                var stored = await storage.TryGetAsync(entry.RowId, cancellationToken).ConfigureAwait(false);
                if (!stored.Found || stored.Row!.Values[columnPosition].IsNull) continue;
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
            EnsureCurrent(); using var lease = await owner.EnterStatementGateAsync(cancellationToken).ConfigureAwait(false);
            HashSet<RowId> rows = [];
            foreach (var key in CatalogIndexKey.EncodeJsonPathValueKeys(definition, path, value))
                rows.UnionWith(await tree.FindAsync(key, cancellationToken).ConfigureAwait(false));
            return rows.OrderBy(row => row.PageId.Value).ThenBy(row => row.SlotId.Value)
                .ThenBy(row => row.Generation).ToArray();
        }

        public async ValueTask<IReadOnlyList<RowId>> FindJsonPathExistsAsync(string path,
            CancellationToken cancellationToken = default)
        {
            EnsureCurrent(); using var lease = await owner.EnterStatementGateAsync(cancellationToken).ConfigureAwait(false);
            HashSet<RowId> rows = [];
            foreach (var key in CatalogIndexKey.EncodeJsonPathExistsKeys(definition, path))
                rows.UnionWith(await tree.FindAsync(key, cancellationToken).ConfigureAwait(false));
            return rows.OrderBy(row => row.PageId.Value).ThenBy(row => row.SlotId.Value)
                .ThenBy(row => row.Generation).ToArray();
        }

        public async ValueTask<IReadOnlyList<RowId>> FindXmlPathAsync(string path, string? value = null,
            CancellationToken cancellationToken = default)
        {
            EnsureCurrent(); using var lease = await owner.EnterStatementGateAsync(cancellationToken).ConfigureAwait(false);
            return await tree.FindAsync(value is null
                ? CatalogIndexKey.EncodeXmlPathExists(definition, path)
                : CatalogIndexKey.EncodeXmlPathValue(definition, path, value), cancellationToken).ConfigureAwait(false);
        }

        private void EnsureCurrent()
        {
            if (_generation != Volatile.Read(ref owner._handleGeneration))
                throw new InvalidOperationException("The storage handle is stale after statement rollback; reopen it from the catalog.");
        }
    }

    private async ValueTask FlushAndPublishAsync(CancellationToken cancellationToken)
    {
        await _bufferPool.FlushAllAsync(cancellationToken).ConfigureAwait(false);
        if (_catalog.RootPageId is { } root && _database.Header.CatalogRootPageId != root)
            await _database.PublishCatalogRootAsync(root, cancellationToken).ConfigureAwait(false);
    }
}
