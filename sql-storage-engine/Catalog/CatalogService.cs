using sql_storage_engine.Buffers;
using sql_storage_engine.Heap;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Pages;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Catalog;

/// <summary>Coordinates validated table metadata with heap allocation and bootstrap catalog publication.</summary>
public sealed class CatalogService
{
    private readonly IPageAllocator _allocator;
    private readonly BufferPool _bufferPool;
    private readonly CatalogPageChain _pageChain;
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
    {
        ArgumentNullException.ThrowIfNull(columns);
        var columnSnapshot = columns.ToArray();
        if (_definition.Tables.Any(table => StringComparer.Ordinal.Equals(table.Name, name)))
            throw new CatalogConflictException($"A table named '{name}' already exists.");
        var nextId = new TableId(_definition.Tables.Count == 0
            ? 1UL
            : checked(_definition.Tables.Max(table => table.Id.Value) + 1));
        // Validate all caller-controlled schema state before allocating any page.
        _ = new CatalogTable(nextId, name, schemaVersion, new PageId(1), columnSnapshot);

        var heap = await TableHeap.CreateAsync(_bufferPool, _allocator, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var table = new CatalogTable(nextId, name, schemaVersion, heap.RootPageId, columnSnapshot);
        var candidate = new CatalogDefinition(_definition.Tables.Append(table), _definition.Indexes);
        try
        {
            var written = await _pageChain.WriteAsync(candidate, cancellationToken).ConfigureAwait(false);
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

    public bool TryOpenTable(string name, out CatalogTable? table)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        table = _definition.Tables.SingleOrDefault(candidate => StringComparer.Ordinal.Equals(candidate.Name, name));
        return table is not null;
    }

    public bool TryOpenTable(TableId id, out CatalogTable? table)
    {
        table = _definition.Tables.SingleOrDefault(candidate => candidate.Id == id);
        return table is not null;
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
    {
        ArgumentNullException.ThrowIfNull(columns);
        if (!TryOpenTable(tableId, out var table)) throw new ArgumentException("Unknown table ID.", nameof(tableId));
        if (_definition.Indexes.Any(index => index.TableId == tableId && StringComparer.Ordinal.Equals(index.Name, name)))
            throw new CatalogConflictException($"An index named '{name}' already exists on the table.");
        var columnSnapshot = columns.ToArray();
        var nextId = new IndexId(_definition.Indexes.Count == 0 ? 1UL : checked(_definition.Indexes.Max(index => index.Id.Value) + 1));
        _ = new CatalogDefinition(_definition.Tables,
            _definition.Indexes.Append(new CatalogIndex(nextId, name, tableId, new PageId(1), isUnique, columnSnapshot)));

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
            await foreach (var entry in heap.ScanAsync(cancellationToken).ConfigureAwait(false))
            {
                var row = RowCodec.Decode(entry.Row.Span, rowTable);
                var provisional = new CatalogIndex(nextId, name, tableId, rootReference.RootPageId, isUnique, columnSnapshot);
                await tree.InsertAsync(CatalogIndexKey.Encode(row, table!, provisional), entry.RowId, cancellationToken)
                    .ConfigureAwait(false);
            }
            var published = new CatalogIndex(nextId, name, tableId, rootReference.RootPageId, isUnique, columnSnapshot);
            var candidate = new CatalogDefinition(_definition.Tables, _definition.Indexes.Append(published));
            var written = await _pageChain.WriteAsync(candidate, cancellationToken).ConfigureAwait(false);
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

    public PersistentBPlusTree OpenIndex(CatalogIndex index)
    {
        ArgumentNullException.ThrowIfNull(index);
        if (!_definition.Indexes.Any(candidate => candidate.Id == index.Id))
            throw new ArgumentException("Index does not belong to this catalog.", nameof(index));
        return new PersistentBPlusTree(_bufferPool, _allocator, new CatalogIndexRootReference(this, index), index.IsUnique);
    }

    internal static TableDefinition ToRowTable(CatalogTable table) => new(table.Columns.Select(column =>
        new ColumnDefinition(column.Id, column.Name, column.Type, column.IsNullable)));

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
                _index.IsUnique, _index.Columns);
            var candidate = new CatalogDefinition(owner._definition.Tables,
                owner._definition.Indexes.Select(index => index.Id == updated.Id ? updated : index));
            // The new tree root must be durable before metadata is allowed to point at it.
            await owner._bufferPool.FlushAllAsync(cancellationToken).ConfigureAwait(false);
            var written = await owner._pageChain.WriteAsync(candidate, cancellationToken).ConfigureAwait(false);
            await owner._bufferPool.FlushAllAsync(cancellationToken).ConfigureAwait(false);
            owner._definition = candidate;
            owner.RootPageId = written.RootPageId;
            _index = updated;
        }
    }
}
