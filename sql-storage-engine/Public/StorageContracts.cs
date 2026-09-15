using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Tables;

namespace sql_storage_engine;

/// <summary>A row and its opaque, generation-safe physical identity.</summary>
public sealed record StoredRow(RowId RowId, Row Row);
/// <summary>A temporal row with an unambiguous current or history table identity.</summary>
public sealed record StorageTemporalRow(TableId SourceTableId, RowId RowId, Row Row);
public sealed record SpecializedIndexMatch(RowId RowId, double Distance);

/// <summary>A complete or leading composite-key bound for an index scan.</summary>
public sealed record StorageIndexBound
{
    private readonly SqlValue[] _values;
    public StorageIndexBound(IEnumerable<SqlValue> values, bool inclusive = true)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = values.ToArray();
        if (_values.Any(value => value is null))
            throw new ArgumentException("Index bounds cannot contain null CLR references.", nameof(values));
        Inclusive = inclusive;
    }
    public IReadOnlyList<SqlValue> Values => Array.AsReadOnly(_values);
    public bool Inclusive { get; }
}

/// <summary>An index range with independently optional lower/upper bounds.</summary>
public sealed record StorageIndexRange(StorageIndexBound? LowerBound = null, StorageIndexBound? UpperBound = null,
    ScanDirection Direction = ScanDirection.Ascending);

/// <summary>Exact cardinality metadata captured from a validated storage snapshot.</summary>
public sealed record StorageColumnStatistics(ColumnId ColumnId, long NullCount, long DistinctValueCount);
public sealed record StorageTableStatistics
{
    private readonly StorageColumnStatistics[] _columns;
    public StorageTableStatistics(TableId tableId, long rowCount, int heapPageCount,
        IEnumerable<StorageColumnStatistics> columns, DateTimeOffset capturedAt)
    {
        ArgumentNullException.ThrowIfNull(columns);
        TableId = tableId; RowCount = rowCount; HeapPageCount = heapPageCount;
        _columns = columns.ToArray(); CapturedAt = capturedAt;
    }
    public TableId TableId { get; }
    public long RowCount { get; }
    public int HeapPageCount { get; }
    public IReadOnlyList<StorageColumnStatistics> Columns => Array.AsReadOnly(_columns);
    public DateTimeOffset CapturedAt { get; }
}
public sealed record StorageHistogramBucket
{
    private readonly SqlValue[] _upperBound;
    public StorageHistogramBucket(IEnumerable<SqlValue> upperBound, long rangeRows, long equalRows,
        long distinctRangeValues)
    {
        ArgumentNullException.ThrowIfNull(upperBound);
        _upperBound = upperBound.ToArray();
        RangeRows = rangeRows; EqualRows = equalRows; DistinctRangeValues = distinctRangeValues;
    }
    public IReadOnlyList<SqlValue> UpperBound => Array.AsReadOnly(_upperBound);
    public long RangeRows { get; }
    public long EqualRows { get; }
    public long DistinctRangeValues { get; }
}
public sealed record StorageIndexStatistics
{
    private readonly StorageHistogramBucket[] _histogram;
    public StorageIndexStatistics(IndexId indexId, long entryCount, long distinctKeyCount, int leafPageCount,
        IEnumerable<StorageHistogramBucket> histogram, DateTimeOffset capturedAt)
    {
        ArgumentNullException.ThrowIfNull(histogram);
        IndexId = indexId; EntryCount = entryCount; DistinctKeyCount = distinctKeyCount;
        LeafPageCount = leafPageCount; _histogram = histogram.ToArray(); CapturedAt = capturedAt;
    }
    public IndexId IndexId { get; }
    public long EntryCount { get; }
    public long DistinctKeyCount { get; }
    public int LeafPageCount { get; }
    public IReadOnlyList<StorageHistogramBucket> Histogram => Array.AsReadOnly(_histogram);
    public DateTimeOffset CapturedAt { get; }
}
public sealed record StorageIndexEntry
{
    private readonly IReadOnlyDictionary<ColumnId, SqlValue> _includedValues;
    public StorageIndexEntry(RowId rowId, IReadOnlyDictionary<ColumnId, SqlValue> includedValues)
    {
        ArgumentNullException.ThrowIfNull(includedValues);
        RowId = rowId;
        _includedValues = new System.Collections.ObjectModel.ReadOnlyDictionary<ColumnId, SqlValue>(
            new Dictionary<ColumnId, SqlValue>(includedValues));
    }
    public RowId RowId { get; }
    public IReadOnlyDictionary<ColumnId, SqlValue> IncludedValues => _includedValues;
}

/// <summary>
/// Describes a native SYSTEM table sample. A repeatable seed makes page selection stable while the heap layout is
/// unchanged; omitting it requests a fresh selection for each enumeration.
/// </summary>
public abstract record StorageTableSample
{
    protected StorageTableSample(long? repeatableSeed)
    {
        if (repeatableSeed is < 0)
            throw new ArgumentOutOfRangeException(nameof(repeatableSeed), "A repeatable seed cannot be negative.");
        RepeatableSeed = repeatableSeed;
    }

    public long? RepeatableSeed { get; }
}

/// <summary>Selects each heap page with the requested percentage probability.</summary>
public sealed record StoragePercentTableSample : StorageTableSample
{
    public StoragePercentTableSample(decimal percentage, long? repeatableSeed = null) : base(repeatableSeed)
    {
        if (percentage is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(percentage), "A sample percentage must be from 0 through 100.");
        Percentage = percentage;
    }

    public decimal Percentage { get; }
}

/// <summary>
/// Selects heap pages using a probability derived from the requested approximate row count and the current live count.
/// Page granularity means the result cardinality can be above or below the requested count.
/// </summary>
public sealed record StorageRowsTableSample : StorageTableSample
{
    public StorageRowsTableSample(long rowCount, long? repeatableSeed = null) : base(repeatableSeed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowCount);
        RowCount = rowCount;
    }

    public long RowCount { get; }
}

/// <summary>A typed native FOR SYSTEM_TIME selection over a system-versioned current/history table pair.</summary>
public abstract record StorageTemporalQuery
{
    internal static DateTime NormalizeUtc(DateTime value, string parameterName) => value.Kind switch
    {
        DateTimeKind.Utc => DateTime.SpecifyKind(value, DateTimeKind.Unspecified),
        DateTimeKind.Unspecified => value,
        _ => throw new ArgumentException("Temporal query boundaries must be UTC or have unspecified kind.", parameterName)
    };

    internal static (DateTime Start, DateTime End) NormalizeRange(DateTime start, DateTime end)
    {
        start = NormalizeUtc(start, nameof(start));
        end = NormalizeUtc(end, nameof(end));
        if (start > end) throw new ArgumentException("A temporal range start cannot be after its end.");
        return (start, end);
    }
}

public sealed record StorageTemporalAsOf : StorageTemporalQuery
{
    public StorageTemporalAsOf(DateTime instant) => Instant = NormalizeUtc(instant, nameof(instant));
    public DateTime Instant { get; }
}

public sealed record StorageTemporalFromTo : StorageTemporalQuery
{
    public StorageTemporalFromTo(DateTime start, DateTime end) => (Start, End) = NormalizeRange(start, end);
    public DateTime Start { get; }
    public DateTime End { get; }
}

public sealed record StorageTemporalBetweenAnd : StorageTemporalQuery
{
    public StorageTemporalBetweenAnd(DateTime start, DateTime end) => (Start, End) = NormalizeRange(start, end);
    public DateTime Start { get; }
    public DateTime End { get; }
}

public sealed record StorageTemporalContainedIn : StorageTemporalQuery
{
    public StorageTemporalContainedIn(DateTime start, DateTime end) => (Start, End) = NormalizeRange(start, end);
    public DateTime Start { get; }
    public DateTime End { get; }
}

public sealed record StorageTemporalAll : StorageTemporalQuery;

/// <summary>Options controlling the storage engine's bounded in-memory resources.</summary>
public sealed record StorageEngineOptions
{
    public int PageSize { get; init; } = Pages.PageConstants.DefaultSize;
    public int BufferPoolCapacity { get; init; } = 256;
    public int InlineValueThreshold { get; init; } = 1024;
    public string DefaultCollation { get; init; } = "SQL_Latin1_General_CP1_CI_AS";
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}

/// <summary>Permission context used when projecting dynamically masked query results.</summary>
public sealed record StorageReadOptions
{
    private readonly HashSet<ColumnId> _unmaskedColumns;

    public StorageReadOptions(bool canUnmaskAll = false, IEnumerable<ColumnId>? unmaskedColumns = null)
    {
        CanUnmaskAll = canUnmaskAll;
        _unmaskedColumns = unmaskedColumns?.ToHashSet() ?? [];
    }

    public bool CanUnmaskAll { get; }
    public IReadOnlySet<ColumnId> UnmaskedColumns => _unmaskedColumns;
    public bool CanUnmask(ColumnId columnId) => CanUnmaskAll || _unmaskedColumns.Contains(columnId);
    public static StorageReadOptions Unprivileged { get; } = new();
    public static StorageReadOptions Privileged { get; } = new(canUnmaskAll: true);
}

/// <summary>Read-only catalog operations intended for name binding and semantic analysis.</summary>
public interface IStorageCatalog
{
    DatabaseId DatabaseId { get; }
    IReadOnlyList<CatalogTable> Tables { get; }
    IReadOnlyList<CatalogIndex> Indexes { get; }
    IReadOnlyList<CatalogScalarType> ScalarTypes { get; }
    IReadOnlyList<CatalogTableType> TableTypes { get; }
    IReadOnlyList<SqlXmlSchemaCollection> XmlSchemaCollections { get; }
    IReadOnlyList<CatalogAssembly> Assemblies { get; }
    string DefaultCollation { get; }
    bool TryGetTable(string name, out CatalogTable? table);
    bool TryGetTable(CatalogTableName name, out CatalogTable? table);
    bool TryGetTable(TableId id, out CatalogTable? table);
    bool TryGetTemporalHistory(TableId currentTableId, out CatalogTable? historyTable);
    bool TryGetTemporalCurrent(TableId historyTableId, out CatalogTable? currentTable);
    bool TryGetIndex(TableId tableId, string name, out CatalogIndex? index);
    IReadOnlyList<CatalogIndex> GetIndexes(TableId tableId);
    bool TryGetScalarType(string schemaName, string name, out CatalogScalarType? type);
    bool TryGetTableType(string schemaName, string name, out CatalogTableType? type);
    bool TryGetXmlSchemaCollection(string schemaName, string name, out SqlXmlSchemaCollection? collection);
    bool TryGetAssembly(string name, out CatalogAssembly? assembly);
}

/// <summary>Logical table access for a SQL executor; physical pages and encodings remain hidden.</summary>
public interface IStorageTable
{
    CatalogTable Definition { get; }
    ValueTask<RowId> InsertAsync(Row row, CancellationToken cancellationToken = default);
    ValueTask<TableInsertResult> TryInsertAsync(Row row, CancellationToken cancellationToken = default);
    ValueTask<StoredRow?> GetAsync(RowId rowId, CancellationToken cancellationToken = default);
    ValueTask<StoredRow?> GetAsync(RowId rowId, StorageReadOptions readOptions,
        CancellationToken cancellationToken = default);
    IAsyncEnumerable<StoredRow> ScanAsync(CancellationToken cancellationToken = default);
    IAsyncEnumerable<StoredRow> ScanAsync(StorageReadOptions readOptions,
        CancellationToken cancellationToken = default);
    IAsyncEnumerable<StoredRow> SampleAsync(StorageTableSample sample,
        CancellationToken cancellationToken = default);
    IAsyncEnumerable<StoredRow> SampleAsync(StorageTableSample sample, StorageReadOptions readOptions,
        CancellationToken cancellationToken = default);
    IAsyncEnumerable<StorageTemporalRow> TemporalScanAsync(StorageTemporalQuery query,
        CancellationToken cancellationToken = default);
    IAsyncEnumerable<StorageTemporalRow> TemporalScanAsync(StorageTemporalQuery query, StorageReadOptions readOptions,
        CancellationToken cancellationToken = default);
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
    ValueTask<IReadOnlyList<StorageIndexEntry>> FindEntriesAsync(IReadOnlyList<SqlValue> values,
        CancellationToken cancellationToken = default);
    IAsyncEnumerable<RowId> ScanAsync(IReadOnlyList<SqlValue> lowerBound,
        IReadOnlyList<SqlValue> upperBound, bool includeLowerBound = true, bool includeUpperBound = true,
        ScanDirection direction = ScanDirection.Ascending, CancellationToken cancellationToken = default);
    IAsyncEnumerable<RowId> ScanAsync(StorageIndexRange range,
        CancellationToken cancellationToken = default);
    IAsyncEnumerable<StorageIndexEntry> ScanEntriesAsync(StorageIndexRange range,
        CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<SpecializedIndexMatch>> SearchNearestAsync(SqlValue query, int count,
        CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<RowId>> FindJsonPathAsync(string path, SqlValue value,
        CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<RowId>> FindJsonPathExistsAsync(string path,
        CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<RowId>> FindXmlPathAsync(string path, string? value = null,
        CancellationToken cancellationToken = default);
    IAsyncEnumerable<FullTextIndexMatch> SearchFullTextAsync(FullTextSearchRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>The supported high-level boundary between a SQL engine and this storage package.</summary>
public interface IStorageEngine : IAsyncDisposable
{
    Security.IStorageSecurity Security => throw new NotSupportedException("Security catalog is not supported.");
    ValueTask<IStorageTransaction> BeginTransactionAsync(StorageTransactionOptions options, CancellationToken cancellationToken = default);
    ValueTask<StorageTransactionReceipt> ResolveTransactionAsync(StorageTransactionIdentity identity, CancellationToken cancellationToken = default);
    ValueTask ReleaseTransactionReceiptAsync(StorageTransactionIdentity identity, CancellationToken cancellationToken = default);
    DatabaseId DatabaseId { get; }
    IStorageCatalog Catalog { get; }
    ValueTask<CatalogTable> CreateTableAsync(string name, IEnumerable<CatalogColumn> columns,
        CancellationToken cancellationToken = default);
    ValueTask<CatalogTable> CreateTableAsync(string name, IEnumerable<CatalogColumn> columns,
        IEnumerable<CatalogCheckConstraint> checkConstraints, CancellationToken cancellationToken = default);
    ValueTask<CatalogTable> CreateTableAsync(CatalogTableName name, IEnumerable<CatalogColumn> columns,
        IEnumerable<CatalogCheckConstraint>? checkConstraints = null,
        CancellationToken cancellationToken = default);
    ValueTask<CatalogTable> CreateSystemVersionedTableAsync(CatalogTableName name,
        IEnumerable<CatalogColumn> columns, ColumnId periodStartColumnId, ColumnId periodEndColumnId,
        CatalogTableName? historyTableName = null,
        IEnumerable<CatalogCheckConstraint>? checkConstraints = null,
        CancellationToken cancellationToken = default);
    ValueTask<CatalogIndex> CreateIndexAsync(string name, TableId tableId, bool isUnique,
        IEnumerable<CatalogIndexedColumn> columns, CancellationToken cancellationToken = default);
    ValueTask<CatalogIndex> CreateIndexAsync(string name, TableId tableId, bool isUnique,
        IEnumerable<CatalogIndexedColumn> columns, CatalogBTreeIndexOptions options,
        CancellationToken cancellationToken = default);
    ValueTask<CatalogIndex> CreateSpecializedIndexAsync(string name, TableId tableId, CatalogIndexedColumn column,
        CatalogSpecializedIndexOptions options, CancellationToken cancellationToken = default);
    ValueTask<CatalogTable> RegisterGraphNodeTableAsync(TableId tableId, ColumnId identityColumnId,
        IndexId identityIndexId, CancellationToken cancellationToken = default);
    ValueTask<CatalogTable> RegisterGraphEdgeTableAsync(TableId tableId, ColumnId identityColumnId,
        IndexId identityIndexId, TableId fromNodeTableId, ColumnId fromNodeColumnId, IndexId outgoingIndexId,
        TableId toNodeTableId, ColumnId toNodeColumnId, IndexId incomingIndexId,
        CancellationToken cancellationToken = default);
    ValueTask<CatalogScalarType> CreateScalarTypeAsync(SqlType definition,
        CancellationToken cancellationToken = default);
    ValueTask<CatalogTableType> CreateTableTypeAsync(string schemaName, string name,
        IEnumerable<CatalogColumn> columns, IEnumerable<CatalogTableTypeIndex>? indexes = null,
        CancellationToken cancellationToken = default);
    ValueTask<CatalogTableType> CreateTableTypeAsync(string schemaName, string name,
        IEnumerable<CatalogColumn> columns, IEnumerable<CatalogTableTypeIndex>? indexes,
        IEnumerable<CatalogCheckConstraint>? checkConstraints, bool isMemoryOptimized,
        CancellationToken cancellationToken = default);
    ValueTask<SqlXmlSchemaCollection> CreateXmlSchemaCollectionAsync(SqlXmlSchemaCollection collection,
        CancellationToken cancellationToken = default);
    ValueTask<SqlXmlSchemaCollection> AlterXmlSchemaCollectionAsync(string schemaName, string name,
        IEnumerable<string> additionalDefinitions, CancellationToken cancellationToken = default);
    ValueTask DropXmlSchemaCollectionAsync(string schemaName, string name, CancellationToken cancellationToken = default);
    ValueTask<CatalogAssembly> CreateAssemblyAsync(CatalogAssembly assembly, CancellationToken cancellationToken = default);
    ValueTask DropAssemblyAsync(string name, CancellationToken cancellationToken = default);
    ValueTask<IStorageTable> OpenTableAsync(TableId tableId, CancellationToken cancellationToken = default);
    ValueTask<IStorageIndex> OpenIndexAsync(IndexId indexId, CancellationToken cancellationToken = default);
    ValueTask<IStorageGraphNodeTable> OpenGraphNodeTableAsync(TableId tableId,
        CancellationToken cancellationToken = default);
    ValueTask<IStorageGraphEdgeTable> OpenGraphEdgeTableAsync(TableId tableId,
        CancellationToken cancellationToken = default);
    ValueTask<StorageTableStatistics> GetTableStatisticsAsync(TableId tableId,
        CancellationToken cancellationToken = default);
    ValueTask<StorageIndexStatistics> GetIndexStatisticsAsync(IndexId indexId,
        CancellationToken cancellationToken = default);
    ValueTask ExecuteStatementAsync(Func<IStorageStatement, CancellationToken, ValueTask> operation,
        CancellationToken cancellationToken = default);
    ValueTask FlushAsync(CancellationToken cancellationToken = default);
}
