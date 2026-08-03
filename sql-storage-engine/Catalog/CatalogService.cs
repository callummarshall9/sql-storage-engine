using sql_storage_engine.Buffers;
using sql_storage_engine.Heap;
using sql_storage_engine.Identifiers;
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
}
