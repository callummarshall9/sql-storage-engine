using sql_storage_engine.Buffers;
using sql_storage_engine.Heap;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Pages;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Overflow;

namespace sql_storage_engine.Catalog;

/// <summary>Coordinates validated table metadata with heap allocation and bootstrap catalog publication.</summary>
public sealed partial class CatalogService
{
    private readonly IPageAllocator _allocator;
    private readonly BufferPool _bufferPool;
    private readonly CatalogPageChain _pageChain;
    private readonly SemaphoreSlim _catalogMutationLock = new(1, 1);
    private CatalogDefinition _definition;

    private CatalogService(IPageStore pageStore, IPageAllocator allocator, BufferPool bufferPool,
        CatalogDefinition definition, PageId? rootPageId)
    {
        _allocator = allocator;
        _bufferPool = bufferPool;
        _pageChain = new CatalogPageChain(pageStore, allocator);
        _definition = definition;
        RootPageId = rootPageId;
    }

    /// <summary>Gets the current persisted catalog root, or null before the first table is published.</summary>
    public PageId? RootPageId { get; private set; }

    public IReadOnlyList<CatalogTable> Tables => _definition.Tables;
    public IReadOnlyList<CatalogIndex> Indexes => _definition.Indexes;
    public IReadOnlyList<CatalogScalarType> ScalarTypes => _definition.ScalarTypes;
    public IReadOnlyList<CatalogTableType> TableTypes => _definition.TableTypes;
    public IReadOnlyList<SqlXmlSchemaCollection> XmlSchemaCollections => _definition.XmlSchemaCollections;
    public IReadOnlyList<CatalogAssembly> Assemblies => _definition.Assemblies;

    public static CatalogService CreateEmpty(IPageStore pageStore, IPageAllocator allocator, BufferPool bufferPool)
    {
        ArgumentNullException.ThrowIfNull(pageStore);
        ArgumentNullException.ThrowIfNull(allocator);
        ArgumentNullException.ThrowIfNull(bufferPool);
        return new CatalogService(pageStore, allocator, bufferPool, new CatalogDefinition([], []), null);
    }

    public static async ValueTask<CatalogService> OpenAsync(PageId rootPageId, IPageStore pageStore,
        IPageAllocator allocator, BufferPool bufferPool, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pageStore);
        ArgumentNullException.ThrowIfNull(allocator);
        ArgumentNullException.ThrowIfNull(bufferPool);
        var chain = new CatalogPageChain(pageStore, allocator);
        var definition = await chain.ReadAsync(rootPageId, cancellationToken).ConfigureAwait(false);
        return new CatalogService(pageStore, allocator, bufferPool, definition, rootPageId);
    }

    /// <summary>Creates a heap and publishes its metadata only after validation and persistence succeed.</summary>
    public async ValueTask<CatalogTable> CreateTableAsync(string name, ulong schemaVersion,
        IEnumerable<CatalogColumn> columns, CancellationToken cancellationToken = default)
        => await CreateTableAsync(name, schemaVersion, columns, null, cancellationToken).ConfigureAwait(false);

    public async ValueTask<CatalogTable> CreateTableAsync(string name, ulong schemaVersion,
        IEnumerable<CatalogColumn> columns, IEnumerable<CatalogCheckConstraint>? checkConstraints,
        CancellationToken cancellationToken = default)
        => await CreateTableAsync(new CatalogTableName("default", "dbo", name), schemaVersion, columns,
            checkConstraints, cancellationToken).ConfigureAwait(false);

    public async ValueTask<CatalogTable> CreateTableAsync(CatalogTableName name, ulong schemaVersion,
        IEnumerable<CatalogColumn> columns, IEnumerable<CatalogCheckConstraint>? checkConstraints = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(columns);
        var columnSnapshot = columns.ToArray();
        var checkSnapshot = checkConstraints?.ToArray() ?? [];
        if (_definition.Tables.Any(table => table.QualifiedName == name))
            throw new CatalogConflictException($"A table named {name.QualifiedName} already exists.");
        var nextId = new TableId(_definition.Tables.Count == 0
            ? 1UL
            : checked(_definition.Tables.Max(table => table.Id.Value) + 1));
        // Validate all caller-controlled schema state before allocating any page.
        _ = new CatalogTable(nextId, name.Name, schemaVersion, new PageId(1), columnSnapshot, checkSnapshot,
            databaseName: name.DatabaseName, schemaName: name.SchemaName);

        var heap = await TableHeap.CreateAsync(_bufferPool, _allocator, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var table = new CatalogTable(nextId, name.Name, schemaVersion, heap.RootPageId, columnSnapshot, checkSnapshot,
            databaseName: name.DatabaseName, schemaName: name.SchemaName);
        var candidate = new CatalogDefinition(_definition.Tables.Append(table), _definition.Indexes,
            _definition.ScalarTypes, _definition.TableTypes, _definition.XmlSchemaCollections, _definition.Assemblies);
        try
        {
            var written = await WriteDefinitionAsync(candidate, cancellationToken).ConfigureAwait(false);
            await _bufferPool.FlushAllAsync(cancellationToken).ConfigureAwait(false);
            _definition = candidate;
            RootPageId = written.RootPageId;
            return table;
        }
        catch
        {
            await _bufferPool.DiscardPageAsync(heap.RootPageId, CancellationToken.None).ConfigureAwait(false);
            await _allocator.FreeAsync(heap.RootPageId, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Creates a current/history heap pair and publishes their temporal relationship atomically.</summary>
    public async ValueTask<CatalogTable> CreateSystemVersionedTableAsync(CatalogTableName name,
        CatalogTableName historyName, ulong schemaVersion, IEnumerable<CatalogColumn> columns,
        ColumnId periodStartColumnId, ColumnId periodEndColumnId,
        IEnumerable<CatalogCheckConstraint>? checkConstraints = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(historyName);
        ArgumentNullException.ThrowIfNull(columns);
        if (name == historyName)
            throw new ArgumentException("The current and history tables must have different names.", nameof(historyName));
        if (_definition.Tables.Any(table => table.QualifiedName == name || table.QualifiedName == historyName))
            throw new CatalogConflictException("The current or history table name already exists.");

        var columnSnapshot = columns.ToArray();
        var checkSnapshot = checkConstraints?.ToArray() ?? [];
        var nextValue = _definition.Tables.Count == 0 ? 1UL : checked(_definition.Tables.Max(table => table.Id.Value) + 1);
        var currentId = new TableId(nextValue);
        var historyId = new TableId(checked(nextValue + 1));
        var temporal = new CatalogSystemVersioning(historyId, periodStartColumnId, periodEndColumnId);
        _ = new CatalogTable(currentId, name.Name, schemaVersion, new PageId(1), columnSnapshot, checkSnapshot,
            databaseName: name.DatabaseName, schemaName: name.SchemaName, systemVersioning: temporal);

        TableHeap? currentHeap = null;
        TableHeap? historyHeap = null;
        try
        {
            currentHeap = await TableHeap.CreateAsync(_bufferPool, _allocator, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            historyHeap = await TableHeap.CreateAsync(_bufferPool, _allocator, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var current = new CatalogTable(currentId, name.Name, schemaVersion, currentHeap.RootPageId,
                columnSnapshot, checkSnapshot, databaseName: name.DatabaseName, schemaName: name.SchemaName,
                systemVersioning: temporal);
            var history = new CatalogTable(historyId, historyName.Name, schemaVersion, historyHeap.RootPageId,
                columnSnapshot, databaseName: historyName.DatabaseName, schemaName: historyName.SchemaName);
            var candidate = new CatalogDefinition(_definition.Tables.Append(current).Append(history),
                _definition.Indexes, _definition.ScalarTypes, _definition.TableTypes,
                _definition.XmlSchemaCollections, _definition.Assemblies);
            var written = await WriteDefinitionAsync(candidate, cancellationToken).ConfigureAwait(false);
            await _bufferPool.FlushAllAsync(cancellationToken).ConfigureAwait(false);
            _definition = candidate;
            RootPageId = written.RootPageId;
            return current;
        }
        catch
        {
            foreach (var heap in new[] { historyHeap, currentHeap }.Where(heap => heap is not null))
            {
                await _bufferPool.DiscardPageAsync(heap!.RootPageId, CancellationToken.None).ConfigureAwait(false);
                await _allocator.FreeAsync(heap.RootPageId, CancellationToken.None).ConfigureAwait(false);
            }
            throw;
        }
    }

    public async ValueTask<SqlValue> AllocateIdentityAsync(TableId tableId,
        CancellationToken cancellationToken = default)
    {
        await _catalogMutationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!TryOpenTable(tableId, out var table)) throw new ArgumentException("Unknown table ID.", nameof(tableId));
            var identityColumn = table!.Columns.SingleOrDefault(column => column.Identity is not null) ??
                throw new InvalidOperationException("The table has no identity column.");
            var current = table.NextIdentityValue ?? throw new StorageCorruptionException("Identity allocation state is missing.");
            var allocated = SqlConversion.ConvertTo(identityColumn.Type,
                SqlValue.Decimal(new SqlDecimal(current, 0)));
            var next = current + identityColumn.Identity!.Increment;
            var replacement = new CatalogTable(table.Id, table.Name, table.SchemaVersion, table.FirstHeapPageId,
                table.Columns, table.CheckConstraints, next, table.DatabaseName, table.SchemaName,
                table.SystemVersioning, table.Graph)
            { ObjectId = table.ObjectId };
            var candidate = new CatalogDefinition(_definition.Tables.Select(item => item.Id == tableId ? replacement : item),
                _definition.Indexes, _definition.ScalarTypes, _definition.TableTypes,
                _definition.XmlSchemaCollections, _definition.Assemblies);
            var written = await WriteDefinitionAsync(candidate, cancellationToken).ConfigureAwait(false);
            await _bufferPool.FlushAllAsync(cancellationToken).ConfigureAwait(false);
            _definition = candidate; RootPageId = written.RootPageId;
            return allocated;
        }
        finally { _catalogMutationLock.Release(); }
    }

    public bool TryOpenTable(string name, out CatalogTable? table)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        var matches = _definition.Tables.Where(candidate => StringComparer.Ordinal.Equals(candidate.Name, name))
            .Take(2).ToArray();
        table = matches.Length == 1 ? matches[0] : null;
        return table is not null;
    }

    public bool TryOpenTable(CatalogTableName name, out CatalogTable? table)
    {
        ArgumentNullException.ThrowIfNull(name);
        table = _definition.Tables.SingleOrDefault(candidate => candidate.QualifiedName == name);
        return table is not null;
    }

    public bool TryOpenTable(TableId id, out CatalogTable? table)
    {
        table = _definition.Tables.SingleOrDefault(candidate => candidate.Id == id);
        return table is not null;
    }

    public ValueTask<CatalogTable> RegisterGraphNodeTableAsync(TableId tableId, ColumnId identityColumnId,
        IndexId identityIndexId, CancellationToken cancellationToken = default) =>
        RegisterGraphTableAsync(tableId, CatalogGraphTable.Node(identityColumnId, identityIndexId),
            cancellationToken);

    public ValueTask<CatalogTable> RegisterGraphEdgeTableAsync(TableId tableId, ColumnId identityColumnId,
        IndexId identityIndexId, TableId fromNodeTableId, ColumnId fromNodeColumnId, IndexId outgoingIndexId,
        TableId toNodeTableId, ColumnId toNodeColumnId, IndexId incomingIndexId,
        CancellationToken cancellationToken = default) => RegisterGraphTableAsync(tableId,
        CatalogGraphTable.Edge(identityColumnId, identityIndexId, fromNodeTableId, fromNodeColumnId,
            outgoingIndexId, toNodeTableId, toNodeColumnId, incomingIndexId), cancellationToken);

    private async ValueTask<CatalogTable> RegisterGraphTableAsync(TableId tableId, CatalogGraphTable graph,
        CancellationToken cancellationToken)
    {
        await _catalogMutationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!TryOpenTable(tableId, out var table)) throw new ArgumentException("Unknown table ID.", nameof(tableId));
            if (table!.Graph is not null)
                throw new CatalogConflictException($"Table {table.QualifiedName} is already registered for graph storage.");
            var replacement = new CatalogTable(table.Id, table.Name, checked(table.SchemaVersion + 1),
                table.FirstHeapPageId, table.Columns, table.CheckConstraints, table.NextIdentityValue,
                table.DatabaseName, table.SchemaName, table.SystemVersioning, graph)
            { ObjectId = table.ObjectId };
            var candidate = new CatalogDefinition(_definition.Tables.Select(item => item.Id == tableId ? replacement : item),
                _definition.Indexes, _definition.ScalarTypes, _definition.TableTypes,
                _definition.XmlSchemaCollections, _definition.Assemblies);
            await PublishDefinitionAsync(candidate, cancellationToken).ConfigureAwait(false);
            return replacement;
        }
        finally { _catalogMutationLock.Release(); }
    }

    public ValueTask<TableHeap> OpenHeapAsync(CatalogTable table, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!_definition.Tables.Any(candidate => candidate.Id == table.Id))
            throw new ArgumentException("Table does not belong to this catalog.", nameof(table));
        return TableHeap.OpenAsync(table.FirstHeapPageId, _bufferPool, _allocator,
            cancellationToken: cancellationToken);
    }

    /// <summary>Builds an index from all live rows and publishes it only after a successful, flushed build.</summary>
    public async ValueTask<CatalogIndex> CreateIndexAsync(string name, TableId tableId, bool isUnique,
        IEnumerable<CatalogIndexedColumn> columns, CancellationToken cancellationToken = default)
        => await CreateIndexCoreAsync(name, tableId, isUnique, columns, null, null, cancellationToken).ConfigureAwait(false);

    public async ValueTask<CatalogIndex> CreateIndexAsync(string name, TableId tableId, bool isUnique,
        IEnumerable<CatalogIndexedColumn> columns, CatalogBTreeIndexOptions options,
        CancellationToken cancellationToken = default)
        => await CreateIndexCoreAsync(name, tableId, isUnique, columns, null, options, cancellationToken).ConfigureAwait(false);

    public async ValueTask<CatalogIndex> CreateSpecializedIndexAsync(string name, TableId tableId,
        CatalogIndexedColumn column, CatalogSpecializedIndexOptions options,
        CancellationToken cancellationToken = default)
        => await CreateIndexCoreAsync(name, tableId, false, [column], options, null, cancellationToken).ConfigureAwait(false);

    private async ValueTask<CatalogIndex> CreateIndexCoreAsync(string name, TableId tableId, bool isUnique,
        IEnumerable<CatalogIndexedColumn> columns, CatalogSpecializedIndexOptions? specializedOptions,
        CatalogBTreeIndexOptions? btreeOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(columns);
        if (!TryOpenTable(tableId, out var table)) throw new ArgumentException("Unknown table ID.", nameof(tableId));
        if (_definition.Indexes.Any(index => index.TableId == tableId && StringComparer.Ordinal.Equals(index.Name, name)))
            throw new CatalogConflictException($"An index named '{name}' already exists on the table.");
        var columnSnapshot = columns.ToArray();
        btreeOptions ??= new CatalogBTreeIndexOptions();
        var nextId = new IndexId(_definition.Indexes.Count == 0 ? 1UL : checked(_definition.Indexes.Max(index => index.Id.Value) + 1));
        _ = new CatalogDefinition(_definition.Tables,
            _definition.Indexes.Append(new CatalogIndex(nextId, name, tableId, new PageId(1), isUnique, columnSnapshot, specializedOptions,
                btreeOptions.StorageKind, btreeOptions.IsPrimaryKey, btreeOptions.IncludedColumns, btreeOptions.IgnoreDuplicateKey)),
            _definition.ScalarTypes, _definition.TableTypes, _definition.XmlSchemaCollections, _definition.Assemblies);

        var recording = new RecordingAllocator(_allocator);
        try
        {
            var root = await recording.AllocateAsync(PageType.BPlusTreeLeaf, cancellationToken).ConfigureAwait(false);
            using (var pin = await _bufferPool.GetPageAsync(root, cancellationToken).ConfigureAwait(false))
            {
                LeafIndexPageCodec.Write(pin.Memory.Span, new LeafIndexPage(root, null, null, null, []));
                pin.MarkDirty(new LogSequenceNumber(0));
            }
            var rootReference = new MutableIndexRootReference(root);
            var tree = new PersistentBPlusTree(_bufferPool, recording, rootReference, isUnique);
            var heap = await OpenHeapAsync(table!, cancellationToken).ConfigureAwait(false);
            var rowTable = ToRowTable(table!);
            var rowCodec = new OverflowRowCodec(new OverflowManager(_bufferPool, _allocator), 0);
            var vectorValueCount = 0;
            var vectorColumnPosition = specializedOptions?.Method == CatalogIndexMethod.Vector
                ? table!.Columns.Select((column, position) => (column, position))
                    .Single(item => item.column.Id == columnSnapshot.Single().ColumnId).position
                : -1;
            await foreach (var entry in heap.ScanAsync(cancellationToken).ConfigureAwait(false))
            {
                var row = await rowCodec.DecodeAsync(entry.Row, rowTable, cancellationToken).ConfigureAwait(false);
                if (vectorColumnPosition >= 0 && !row.Values[vectorColumnPosition].IsNull) vectorValueCount++;
                var provisional = new CatalogIndex(nextId, name, tableId, rootReference.RootPageId, isUnique, columnSnapshot, specializedOptions,
                    btreeOptions.StorageKind, btreeOptions.IsPrimaryKey, btreeOptions.IncludedColumns, btreeOptions.IgnoreDuplicateKey);
                foreach (var key in CatalogIndexKey.EncodeEntries(row, table!, provisional))
                    await tree.InsertAsync(key, entry.RowId,
                        CatalogIndexKey.EncodeIncludedValues(row, table!, provisional),
                        cancellationToken).ConfigureAwait(false);
            }
            if (vectorColumnPosition >= 0 && vectorValueCount < 100)
                throw new InvalidOperationException("A vector index requires at least 100 non-NULL vectors when it is created.");
            var published = new CatalogIndex(nextId, name, tableId, rootReference.RootPageId, isUnique, columnSnapshot, specializedOptions,
                btreeOptions.StorageKind, btreeOptions.IsPrimaryKey, btreeOptions.IncludedColumns, btreeOptions.IgnoreDuplicateKey);
            var candidate = new CatalogDefinition(_definition.Tables, _definition.Indexes.Append(published),
                _definition.ScalarTypes, _definition.TableTypes, _definition.XmlSchemaCollections, _definition.Assemblies);
            var written = await WriteDefinitionAsync(candidate, cancellationToken).ConfigureAwait(false);
            await _bufferPool.FlushAllAsync(cancellationToken).ConfigureAwait(false);
            _definition = candidate;
            RootPageId = written.RootPageId;
            return published;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var unreclaimed = new List<PageId>();
            foreach (var pageId in recording.Allocated.Reverse())
            {
                try { await _bufferPool.DiscardPageAsync(pageId, CancellationToken.None).ConfigureAwait(false); await _allocator.FreeAsync(pageId, CancellationToken.None).ConfigureAwait(false); }
                catch (StorageException) { unreclaimed.Add(pageId); }
            }
            throw new IndexBuildException("Secondary-index build failed before publication.", recording.Allocated, unreclaimed, exception);
        }
    }

    public bool TryOpenIndex(string name, TableId tableId, out CatalogIndex? index)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        index = _definition.Indexes.SingleOrDefault(candidate => candidate.TableId == tableId && StringComparer.Ordinal.Equals(candidate.Name, name));
        return index is not null;
    }

    public async ValueTask<CatalogScalarType> CreateScalarTypeAsync(SqlType definition,
        CancellationToken cancellationToken = default)
    {
        var type = new CatalogScalarType(definition);
        EnsureTypeNameAvailable(type.SchemaName, type.Name);
        var candidate = new CatalogDefinition(_definition.Tables, _definition.Indexes,
            _definition.ScalarTypes.Append(type), _definition.TableTypes, _definition.XmlSchemaCollections, _definition.Assemblies);
        await PublishDefinitionAsync(candidate, cancellationToken).ConfigureAwait(false);
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
        var type = new CatalogTableType(schemaName, name, columns, indexes, checkConstraints, isMemoryOptimized);
        EnsureTypeNameAvailable(type.SchemaName, type.Name);
        var candidate = new CatalogDefinition(_definition.Tables, _definition.Indexes,
            _definition.ScalarTypes, _definition.TableTypes.Append(type), _definition.XmlSchemaCollections, _definition.Assemblies);
        await PublishDefinitionAsync(candidate, cancellationToken).ConfigureAwait(false);
        return type;
    }

    public bool TryOpenScalarType(string schemaName, string name, out CatalogScalarType? type)
    {
        type = _definition.ScalarTypes.SingleOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.SchemaName, schemaName) &&
            StringComparer.Ordinal.Equals(candidate.Name, name));
        return type is not null;
    }

    public bool TryOpenTableType(string schemaName, string name, out CatalogTableType? type)
    {
        type = _definition.TableTypes.SingleOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.SchemaName, schemaName) &&
            StringComparer.Ordinal.Equals(candidate.Name, name));
        return type is not null;
    }

    public bool TryOpenXmlSchemaCollection(string schemaName, string name, out SqlXmlSchemaCollection? collection)
    {
        collection = _definition.XmlSchemaCollections.SingleOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.SchemaName, schemaName) && StringComparer.Ordinal.Equals(candidate.Name, name));
        return collection is not null;
    }

    public bool TryOpenAssembly(string name, out CatalogAssembly? assembly)
    {
        assembly = _definition.Assemblies.SingleOrDefault(candidate => StringComparer.Ordinal.Equals(candidate.Name, name));
        return assembly is not null;
    }

    public async ValueTask<SqlXmlSchemaCollection> CreateXmlSchemaCollectionAsync(SqlXmlSchemaCollection collection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collection);
        if (TryOpenXmlSchemaCollection(collection.SchemaName, collection.Name, out _))
            throw new CatalogConflictException($"XML schema collection {collection.QualifiedName} already exists.");
        var candidate = new CatalogDefinition(_definition.Tables, _definition.Indexes, _definition.ScalarTypes,
            _definition.TableTypes, _definition.XmlSchemaCollections.Append(collection), _definition.Assemblies);
        await PublishDefinitionAsync(candidate, cancellationToken).ConfigureAwait(false);
        return collection;
    }

    public async ValueTask<SqlXmlSchemaCollection> AlterXmlSchemaCollectionAsync(string schemaName, string name,
        IEnumerable<string> additionalDefinitions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(additionalDefinitions);
        if (!TryOpenXmlSchemaCollection(schemaName, name, out var existing))
            throw new KeyNotFoundException($"Unknown XML schema collection [{schemaName}].[{name}].");
        var updated = new SqlXmlSchemaCollection(schemaName, name, existing!.Definitions.Concat(additionalDefinitions));
        var candidate = RebindXmlCollection(existing, updated);
        await PublishDefinitionAsync(candidate, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public async ValueTask DropXmlSchemaCollectionAsync(string schemaName, string name,
        CancellationToken cancellationToken = default)
    {
        if (!TryOpenXmlSchemaCollection(schemaName, name, out var existing))
            throw new KeyNotFoundException($"Unknown XML schema collection [{schemaName}].[{name}].");
        if (AllColumns(_definition).Any(column => column.Type.XmlSchemaCollection?.Equals(existing) == true))
            throw new CatalogConflictException($"XML schema collection {existing!.QualifiedName} is referenced by a column.");
        var candidate = new CatalogDefinition(_definition.Tables, _definition.Indexes, _definition.ScalarTypes,
            _definition.TableTypes, _definition.XmlSchemaCollections.Where(item => !item.Equals(existing)), _definition.Assemblies);
        await PublishDefinitionAsync(candidate, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<CatalogAssembly> CreateAssemblyAsync(CatalogAssembly assembly,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        if (TryOpenAssembly(assembly.Name, out _)) throw new CatalogConflictException($"Assembly '{assembly.Name}' already exists.");
        var candidate = new CatalogDefinition(_definition.Tables, _definition.Indexes, _definition.ScalarTypes,
            _definition.TableTypes, _definition.XmlSchemaCollections, _definition.Assemblies.Append(assembly));
        await PublishDefinitionAsync(candidate, cancellationToken).ConfigureAwait(false);
        return assembly;
    }

    public async ValueTask DropAssemblyAsync(string name, CancellationToken cancellationToken = default)
    {
        if (!TryOpenAssembly(name, out _)) throw new KeyNotFoundException($"Unknown assembly '{name}'.");
        if (_definition.ScalarTypes.Any(type => type.Definition.UserTypeKind == SqlUserTypeKind.Clr &&
                                                StringComparer.Ordinal.Equals(type.Definition.ClrAssemblyName, name)))
            throw new CatalogConflictException($"Assembly '{name}' is referenced by a CLR type.");
        var candidate = new CatalogDefinition(_definition.Tables, _definition.Indexes, _definition.ScalarTypes,
            _definition.TableTypes, _definition.XmlSchemaCollections,
            _definition.Assemblies.Where(assembly => !StringComparer.Ordinal.Equals(assembly.Name, name)));
        await PublishDefinitionAsync(candidate, cancellationToken).ConfigureAwait(false);
    }

    public PersistentBPlusTree OpenIndex(CatalogIndex index)
    {
        ArgumentNullException.ThrowIfNull(index);
        if (!_definition.Indexes.Any(candidate => candidate.Id == index.Id))
            throw new ArgumentException("Index does not belong to this catalog.", nameof(index));
        return new PersistentBPlusTree(_bufferPool, _allocator, new CatalogIndexRootReference(this, index), index.IsUnique);
    }

    internal static TableDefinition ToRowTable(CatalogTable table) => new(table.Columns.Select(column =>
        new ColumnDefinition(column.Id, column.Name, column.Type, column.IsNullable, column.Encryption,
            column.IsSparse, column.IsColumnSet)));

    private sealed class RecordingAllocator(IPageAllocator inner) : IPageAllocator
    {
        private readonly List<PageId> _allocated = [];
        public IReadOnlyList<PageId> Allocated => _allocated.AsReadOnly();
        public async ValueTask<PageId> AllocateAsync(PageType type, CancellationToken token = default)
        { var id = await inner.AllocateAsync(type, token).ConfigureAwait(false); _allocated.Add(id); return id; }
        public ValueTask FreeAsync(PageId id, CancellationToken token = default) => inner.FreeAsync(id, token);
    }

    /// <summary>Keeps a root created by a later B+ tree split reachable after reopen.</summary>
    private sealed class CatalogIndexRootReference(CatalogService owner, CatalogIndex initial) : IIndexRootReference
    {
        private CatalogIndex _index = initial;
        public PageId RootPageId => _index.RootPageId;

        public async ValueTask UpdateRootAsync(PageId rootPageId, CancellationToken cancellationToken = default)
        {
            if (rootPageId.Value == 0) throw new ArgumentOutOfRangeException(nameof(rootPageId));
            var updated = new CatalogIndex(_index.Id, _index.Name, _index.TableId, rootPageId,
                _index.IsUnique, _index.Columns, _index.SpecializedOptions, _index.StorageKind,
                _index.IsPrimaryKey, _index.IncludedColumns, _index.IgnoreDuplicateKey);
            var candidate = new CatalogDefinition(owner._definition.Tables,
                owner._definition.Indexes.Select(index => index.Id == updated.Id ? updated : index),
                owner._definition.ScalarTypes, owner._definition.TableTypes,
                owner._definition.XmlSchemaCollections, owner._definition.Assemblies);
            // The new tree root must be durable before metadata is allowed to point at it.
            await owner._bufferPool.FlushAllAsync(cancellationToken).ConfigureAwait(false);
            var written = await owner.WriteDefinitionAsync(candidate, cancellationToken).ConfigureAwait(false);
            await owner._bufferPool.FlushAllAsync(cancellationToken).ConfigureAwait(false);
            owner._definition = candidate;
            owner.RootPageId = written.RootPageId;
            _index = updated;
        }
    }

    private void EnsureTypeNameAvailable(string schemaName, string name)
    {
        if (_definition.ScalarTypes.Any(type => StringComparer.Ordinal.Equals(type.SchemaName, schemaName) &&
                                              StringComparer.Ordinal.Equals(type.Name, name)) ||
            _definition.TableTypes.Any(type => StringComparer.Ordinal.Equals(type.SchemaName, schemaName) &&
                                             StringComparer.Ordinal.Equals(type.Name, name)))
            throw new CatalogConflictException($"A type named '[{schemaName}].[{name}]' already exists.");
    }

    private async ValueTask PublishDefinitionAsync(CatalogDefinition candidate,
        CancellationToken cancellationToken)
    {
        var written = await WriteDefinitionAsync(candidate, cancellationToken).ConfigureAwait(false);
        await _bufferPool.FlushAllAsync(cancellationToken).ConfigureAwait(false);
        _definition = candidate;
        RootPageId = written.RootPageId;
    }

    private CatalogDefinition RebindXmlCollection(SqlXmlSchemaCollection existing, SqlXmlSchemaCollection updated)
    {
        CatalogColumn RebindColumn(CatalogColumn column)
        {
            var type = column.Type.XmlSchemaCollection?.Equals(existing) == true
                ? column.Type.RebindXmlSchemaCollection(updated) : column.Type;
            return new CatalogColumn(column.Id, column.Name, type, column.IsNullable, column.DefaultExpression,
                column.Identity, column.IsRowGuidCol, column.ComputedExpression, column.IsComputedPersisted,
                column.IsSparse, column.IsColumnSet, column.IsFileStream, column.MaskingFunction, column.Encryption,
                column.GeneratedAlways, column.IsHidden);
        }
        var tables = _definition.Tables.Select(table => new CatalogTable(table.Id, table.Name, table.SchemaVersion,
            table.FirstHeapPageId, table.Columns.Select(RebindColumn), table.CheckConstraints, table.NextIdentityValue,
            table.DatabaseName, table.SchemaName, table.SystemVersioning, table.Graph)
        { ObjectId = table.ObjectId });
        var tableTypes = _definition.TableTypes.Select(type => new CatalogTableType(type.SchemaName, type.Name,
            type.Columns.Select(RebindColumn), type.Indexes, type.CheckConstraints, type.IsMemoryOptimized));
        return new CatalogDefinition(tables, _definition.Indexes, _definition.ScalarTypes, tableTypes,
            _definition.XmlSchemaCollections.Select(item => item.Equals(existing) ? updated : item), _definition.Assemblies);
    }

    private static IEnumerable<CatalogColumn> AllColumns(CatalogDefinition definition) =>
        definition.Tables.SelectMany(table => table.Columns).Concat(definition.TableTypes.SelectMany(type => type.Columns));
}
