using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;
using System.Numerics;
using System.Text.Json;
using System.Xml;
using System.Xml.XPath;

namespace sql_storage_engine.Catalog;

public sealed record CatalogTable
{
    public Guid ObjectId { get; internal init; } = Guid.NewGuid();
    private readonly CatalogColumn[] _columns;
    private readonly CatalogCheckConstraint[] _checkConstraints;

    public CatalogTable(TableId id, string name, ulong schemaVersion, PageId firstHeapPageId,
        IEnumerable<CatalogColumn> columns, IEnumerable<CatalogCheckConstraint>? checkConstraints = null,
        BigInteger? nextIdentityValue = null, string databaseName = "default", string schemaName = "dbo",
        CatalogSystemVersioning? systemVersioning = null, CatalogGraphTable? graph = null)
    {
        var qualifiedName = new CatalogTableName(databaseName, schemaName, name);
        if (schemaVersion == 0) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        ArgumentNullException.ThrowIfNull(columns);
        _columns = columns.ToArray();
        _checkConstraints = checkConstraints?.ToArray() ?? [];
        if (_columns.Length == 0) throw new ArgumentException("A table must define at least one column.", nameof(columns));
        if (_columns.Length > 30_000)
            throw new ArgumentException("A SQL Server table cannot contain more than 30,000 columns.", nameof(columns));
        if (_columns.Length > 1_024 && (!_columns.Any(column => column.IsColumnSet) ||
                                        _columns.Count(column => !column.IsSparse || column.IsComputed) > 1_024))
            throw new ArgumentException("Tables wider than 1,024 columns require a column set and may have at most 1,024 non-sparse or computed columns.", nameof(columns));
        ValidateUnique(_columns.Select(column => column.Id), "Column IDs", nameof(columns));
        ValidateUnique(_columns.Select(column => column.Name), "Column names", nameof(columns), StringComparer.Ordinal);
        ValidateUnique(_checkConstraints.Select(check => check.Name), "Check-constraint names", nameof(checkConstraints), StringComparer.Ordinal);
        if (_columns.Count(column => column.Type.Name is SqlTypeName.RowVersion or SqlTypeName.Timestamp) > 1)
            throw new ArgumentException("A table can contain only one rowversion column.", nameof(columns));
        if (_columns.Count(column => column.Identity is not null) > 1)
            throw new ArgumentException("A table can contain only one identity column.", nameof(columns));
        if (_columns.Count(column => column.IsRowGuidCol) > 1)
            throw new ArgumentException("A table can contain only one ROWGUIDCOL column.", nameof(columns));
        if (_columns.Count(column => column.IsColumnSet) > 1)
            throw new ArgumentException("A table can contain only one XML column set.", nameof(columns));
        var columnSet = _columns.SingleOrDefault(column => column.IsColumnSet);
        if (columnSet is not null && _columns.Any(column => column.IsSparse && column.MaskingFunction is not null))
            throw new ArgumentException("A SPARSE column participating in a column set cannot be dynamically masked.", nameof(columns));
        if (columnSet is not null && _columns.Any(column => column.ComputedExpression is { } expression &&
                ExpressionReferencesColumn(expression, columnSet.Name)))
            throw new ArgumentException("Computed columns cannot reference an XML column set.", nameof(columns));
        foreach (var masked in _columns.Where(column => column.MaskingFunction is not null))
            if (_columns.Any(column => column.ComputedExpression is { } expression &&
                                      ExpressionReferencesColumn(expression, masked.Name)))
                throw new ArgumentException("A dynamically masked column cannot have a computed-column dependency.", nameof(columns));
        if (_columns.Any(column => column.IsFileStream) && !_columns.Any(column => column.IsRowGuidCol && !column.IsNullable))
            throw new ArgumentException("FILESTREAM requires a non-null ROWGUIDCOL column.", nameof(columns));
        foreach (var vector in _columns.Where(column => column.Type.Name == SqlTypeName.Vector))
            if (_checkConstraints.Any(check => ExpressionReferencesColumn(check.Expression, vector.Name)))
                throw new ArgumentException("SQL Server vector columns do not support CHECK constraints.", nameof(checkConstraints));
        foreach (var column in _columns.Where(column => column.DefaultExpression is not null))
            if (_columns.Any(candidate => ExpressionReferencesColumn(column.DefaultExpression!, candidate.Name)))
                throw new ArgumentException($"DEFAULT expression for column '{column.Name}' cannot reference a table column.", nameof(columns));
        _ = GetComputedColumnOrder(_columns);
        var identity = _columns.SingleOrDefault(column => column.Identity is not null)?.Identity;
        if (identity is null && nextIdentityValue is not null)
            throw new ArgumentException("A table without IDENTITY cannot have identity allocation state.", nameof(nextIdentityValue));
        if (systemVersioning is not null)
        {
            var start = _columns.SingleOrDefault(column => column.Id == systemVersioning.PeriodStartColumnId)
                ?? throw new ArgumentException("The temporal period start column does not belong to the table.", nameof(systemVersioning));
            var end = _columns.SingleOrDefault(column => column.Id == systemVersioning.PeriodEndColumnId)
                ?? throw new ArgumentException("The temporal period end column does not belong to the table.", nameof(systemVersioning));
            if (start.GeneratedAlways != CatalogGeneratedAlwaysKind.RowStart ||
                end.GeneratedAlways != CatalogGeneratedAlwaysKind.RowEnd ||
                start.Type.Name != SqlTypeName.DateTime2 || end.Type.Name != SqlTypeName.DateTime2 || start.Type != end.Type ||
                start.IsNullable || end.IsNullable)
                throw new ArgumentException(
                    "A temporal period requires non-null datetime2 columns generated always as ROW START and ROW END.",
                    nameof(systemVersioning));
        }
        Id = id;
        Name = qualifiedName.Name;
        DatabaseName = qualifiedName.DatabaseName;
        SchemaName = qualifiedName.SchemaName;
        SchemaVersion = schemaVersion;
        FirstHeapPageId = firstHeapPageId;
        NextIdentityValue = identity is null ? null : nextIdentityValue ?? identity.Seed;
        SystemVersioning = systemVersioning;
        Graph = graph;
    }

    public TableId Id { get; }
    public string Name { get; }
    public string DatabaseName { get; }
    public string SchemaName { get; }
    public CatalogTableName QualifiedName => new(DatabaseName, SchemaName, Name);
    public ulong SchemaVersion { get; }
    public PageId FirstHeapPageId { get; }
    public IReadOnlyList<CatalogColumn> Columns => Array.AsReadOnly(_columns);
    public IReadOnlyList<CatalogCheckConstraint> CheckConstraints => Array.AsReadOnly(_checkConstraints);
    public BigInteger? NextIdentityValue { get; }
    public CatalogSystemVersioning? SystemVersioning { get; }
    public CatalogGraphTable? Graph { get; }

    internal static void ValidateUnique<T>(IEnumerable<T> values, string description, string parameterName,
        IEqualityComparer<T>? comparer = null)
    {
        var materialized = values.ToArray();
        if (materialized.Distinct(comparer).Count() != materialized.Length)
            throw new ArgumentException($"{description} must be unique within their scope.", parameterName);
    }

    internal static bool ExpressionReferencesColumn(string expression, string columnName)
    {
        ArgumentNullException.ThrowIfNull(expression); ArgumentNullException.ThrowIfNull(columnName);
        foreach (var identifier in ExpressionIdentifiers(expression))
            if (identifier.Equals(columnName, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    internal static IReadOnlyList<int> GetComputedColumnOrder(IReadOnlyList<CatalogColumn> columns)
    {
        var computed = columns.Select((column, position) => (column, position))
            .Where(item => item.column.IsComputed).ToArray();
        if (computed.Length == 0) return [];
        var computedNames = computed.Select(item => item.column.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dependencies = computed.ToDictionary(item => item.position, item => computedNames
            .Where(name => ExpressionReferencesColumn(item.column.ComputedExpression!, name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase));
        var completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var order = new List<int>(computed.Length);
        while (order.Count != computed.Length)
        {
            var progressed = false;
            foreach (var item in computed.Where(item => !order.Contains(item.position)))
            {
                if (!dependencies[item.position].IsSubsetOf(completed)) continue;
                order.Add(item.position); completed.Add(item.column.Name); progressed = true;
            }
            if (!progressed)
                throw new ArgumentException("Computed-column dependencies contain a cycle.", nameof(columns));
        }
        return order;
    }

    internal static CatalogColumn? ResolveDirectColumnReference(string expression,
        IReadOnlyList<CatalogColumn> columns)
    {
        var token = expression.Trim();
        if (token.Length >= 2 && token[0] == '[' && token[^1] == ']')
            token = token[1..^1].Replace("]]", "]", StringComparison.Ordinal);
        return columns.SingleOrDefault(column => column.Name.Equals(token, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> ExpressionIdentifiers(string expression)
    {
        for (var position = 0; position < expression.Length;)
        {
            if (expression[position] == '\'')
            {
                position++;
                while (position < expression.Length)
                {
                    if (expression[position++] != '\'') continue;
                    if (position < expression.Length && expression[position] == '\'') { position++; continue; }
                    break;
                }
                continue;
            }
            if (expression[position] == '[')
            {
                var identifier = new System.Text.StringBuilder(); position++;
                while (position < expression.Length)
                {
                    if (expression[position] != ']') { identifier.Append(expression[position++]); continue; }
                    position++;
                    if (position < expression.Length && expression[position] == ']')
                    { identifier.Append(']'); position++; continue; }
                    break;
                }
                if (identifier.Length != 0) yield return identifier.ToString();
                continue;
            }
            if (!char.IsLetter(expression[position]) && expression[position] is not ('_' or '@'))
            { position++; continue; }
            var start = position++;
            while (position < expression.Length && (char.IsLetterOrDigit(expression[position]) ||
                   expression[position] is '_' or '@' or '$')) position++;
            var token = expression[start..position];
            var next = position; while (next < expression.Length && char.IsWhiteSpace(expression[next])) next++;
            if (next < expression.Length && expression[next] == '(' || token.Equals("CURRENT_TIMESTAMP", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("NULL", StringComparison.OrdinalIgnoreCase) || token.Equals("AND", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("OR", StringComparison.OrdinalIgnoreCase) || token.Equals("NOT", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("IS", StringComparison.OrdinalIgnoreCase)) continue;
            yield return token;
        }
    }
}

/// <summary>Identifies one table column and its complete index ordering configuration.</summary>
