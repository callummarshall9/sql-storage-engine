using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Catalog;

/// <summary>Defines whether an indexed column is ordered in ascending or descending order.</summary>
public enum SortDirection : byte
{
    Ascending = 1,
    Descending = 2
}

/// <summary>Defines where SQL NULL values sort relative to non-NULL values.</summary>
public enum NullSortOrder : byte
{
    First = 1,
    Last = 2
}

/// <summary>A stable catalog record for one table column.</summary>
public sealed record CatalogColumn
{
    public CatalogColumn(ColumnId id, string name, SqlType type, bool isNullable)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Column name cannot be empty.", nameof(name));
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        Id = id;
        Name = name;
        Type = type;
        IsNullable = isNullable;
    }

    public ColumnId Id { get; }
    public string Name { get; }
    public SqlType Type { get; }
    public bool IsNullable { get; }
}

/// <summary>A stable catalog record for a table and its heap entry point.</summary>
public sealed record CatalogTable
{
    private readonly CatalogColumn[] _columns;

    public CatalogTable(TableId id, string name, ulong schemaVersion, PageId firstHeapPageId,
        IEnumerable<CatalogColumn> columns)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Table name cannot be empty.", nameof(name));
        if (schemaVersion == 0) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        ArgumentNullException.ThrowIfNull(columns);
        _columns = columns.ToArray();
        if (_columns.Length == 0) throw new ArgumentException("A table must define at least one column.", nameof(columns));
        ValidateUnique(_columns.Select(column => column.Id), "Column IDs", nameof(columns));
        ValidateUnique(_columns.Select(column => column.Name), "Column names", nameof(columns), StringComparer.Ordinal);
        Id = id;
        Name = name;
        SchemaVersion = schemaVersion;
        FirstHeapPageId = firstHeapPageId;
    }

    public TableId Id { get; }
    public string Name { get; }
    public ulong SchemaVersion { get; }
    public PageId FirstHeapPageId { get; }
    public IReadOnlyList<CatalogColumn> Columns => Array.AsReadOnly(_columns);

    internal static void ValidateUnique<T>(IEnumerable<T> values, string description, string parameterName,
        IEqualityComparer<T>? comparer = null)
    {
        var materialized = values.ToArray();
        if (materialized.Distinct(comparer).Count() != materialized.Length)
            throw new ArgumentException($"{description} must be unique within their scope.", parameterName);
    }
}

/// <summary>Identifies one table column and its complete index ordering configuration.</summary>
public sealed record CatalogIndexedColumn
{
    public CatalogIndexedColumn(ColumnId columnId, SortDirection direction, NullSortOrder nullSortOrder,
        string? collation = null)
    {
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));
        if (!Enum.IsDefined(nullSortOrder)) throw new ArgumentOutOfRangeException(nameof(nullSortOrder));
        if (collation is not null && string.IsNullOrWhiteSpace(collation))
            throw new ArgumentException("Collation cannot be empty when specified.", nameof(collation));
        ColumnId = columnId;
        Direction = direction;
        NullSortOrder = nullSortOrder;
        Collation = collation;
    }

    public ColumnId ColumnId { get; }
    public SortDirection Direction { get; }
    public NullSortOrder NullSortOrder { get; }
    public string? Collation { get; }
}

/// <summary>A stable catalog record for a composite index.</summary>
public sealed record CatalogIndex
{
    private readonly CatalogIndexedColumn[] _columns;

    public CatalogIndex(IndexId id, string name, TableId tableId, PageId rootPageId, bool isUnique,
        IEnumerable<CatalogIndexedColumn> columns)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Index name cannot be empty.", nameof(name));
        ArgumentNullException.ThrowIfNull(columns);
        _columns = columns.ToArray();
        if (_columns.Length == 0) throw new ArgumentException("An index must define at least one column.", nameof(columns));
        CatalogTable.ValidateUnique(_columns.Select(column => column.ColumnId), "Indexed column IDs", nameof(columns));
        Id = id;
        Name = name;
        TableId = tableId;
        RootPageId = rootPageId;
        IsUnique = isUnique;
    }

    public IndexId Id { get; }
    public string Name { get; }
    public TableId TableId { get; }
    public PageId RootPageId { get; }
    public bool IsUnique { get; }
    public IReadOnlyList<CatalogIndexedColumn> Columns => Array.AsReadOnly(_columns);
}

/// <summary>Validates the complete set of authoritative table and index records.</summary>
public sealed class CatalogDefinition
{
    private readonly CatalogTable[] _tables;
    private readonly CatalogIndex[] _indexes;

    public CatalogDefinition(IEnumerable<CatalogTable> tables, IEnumerable<CatalogIndex> indexes)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(indexes);
        _tables = tables.ToArray();
        _indexes = indexes.ToArray();
        if (_tables.Any(table => table is null)) throw new ArgumentException("Tables cannot contain null records.", nameof(tables));
        if (_indexes.Any(index => index is null)) throw new ArgumentException("Indexes cannot contain null records.", nameof(indexes));
        CatalogTable.ValidateUnique(_tables.Select(table => table.Id), "Table IDs", nameof(tables));
        CatalogTable.ValidateUnique(_tables.Select(table => table.Name), "Table names", nameof(tables), StringComparer.Ordinal);
        CatalogTable.ValidateUnique(_indexes.Select(index => index.Id), "Index IDs", nameof(indexes));

        var tablesById = _tables.ToDictionary(table => table.Id);
        foreach (var group in _indexes.GroupBy(index => index.TableId))
            CatalogTable.ValidateUnique(group.Select(index => index.Name), "Index names", nameof(indexes), StringComparer.Ordinal);
        foreach (var index in _indexes)
        {
            if (!tablesById.TryGetValue(index.TableId, out var table))
                throw new ArgumentException($"Index '{index.Name}' references an unknown table.", nameof(indexes));
            var columnIds = table.Columns.Select(column => column.Id).ToHashSet();
            if (index.Columns.Any(column => !columnIds.Contains(column.ColumnId)))
                throw new ArgumentException($"Index '{index.Name}' references an unknown column.", nameof(indexes));
        }
    }

    public IReadOnlyList<CatalogTable> Tables => Array.AsReadOnly(_tables);
    public IReadOnlyList<CatalogIndex> Indexes => Array.AsReadOnly(_indexes);
}
