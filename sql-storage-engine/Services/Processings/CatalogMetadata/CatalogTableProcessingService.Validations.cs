// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Rows;
using System;
using sql_storage_engine.Catalog;
using System.Collections.Generic;
using System.Linq;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal sealed partial class CatalogTableProcessingService
{
    private static void ValidateCatalogTable(CatalogTable catalogTable)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogTable);
        var id = catalogTable.IdSource;
        var name = catalogTable.NameSource;
        var schemaVersion = catalogTable.SchemaVersionSource;
        var firstHeapPageId = catalogTable.FirstHeapPageIdSource;
        var columns = catalogTable.ColumnsSource;
        var checkConstraints = catalogTable.CheckConstraintsSource;
        var nextIdentityValue = catalogTable.NextIdentityValueSource;
        var databaseName = catalogTable.DatabaseNameSource;
        var schemaName = catalogTable.SchemaNameSource;
        var systemVersioning = catalogTable.SystemVersioningSource;
        var graph = catalogTable.GraphSource;
        CatalogColumn[] _columns;
        CatalogCheckConstraint[] _checkConstraints;
        var qualifiedName = CreateCatalogTableName(databaseName: databaseName, schemaName: schemaName, name: name);

        if (schemaVersion == 0)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(schemaVersion));
        }

        ArgumentNullException.ThrowIfNull(argument: columns);
        _columns = columns.ToArray();

        _checkConstraints = checkConstraints?.Select(selector: PrepareCatalogCheckConstraint)
            .ToArray() ?? [];

        if (_columns.Length == 0)
        {
            throw new ArgumentException(message: "A table must define at least one column.", paramName: nameof(columns));
        }

        if (_columns.Length > 30_000)
        {
            throw new ArgumentException(message: "A SQL Server table cannot contain more than 30,000 columns.", paramName: nameof(columns));
        }

        if (_columns.Length > 1_024 && (!_columns.Any(predicate: column => column.IsColumnSet) || _columns.Count(predicate: column => !column.IsSparse || column.IsComputed) > 1_024))
        {
            throw new ArgumentException(message: "Tables wider than 1,024 columns require a column set and may have at most 1,024 non-sparse or computed columns.", paramName: nameof(columns));
        }

        ValidateUnique(values: _columns.Select(selector: column => column.Id), description: "Column IDs", parameterName: nameof(columns));
        ValidateUnique(values: _columns.Select(selector: column => column.Name), description: "Column names", parameterName: nameof(columns), comparer: StringComparer.Ordinal);
        ValidateUnique(values: _checkConstraints.Select(selector: check => check.Name), description: "Check-constraint names", parameterName: nameof(checkConstraints), comparer: StringComparer.Ordinal);

        if (_columns.Count(predicate: column => column.Type.Name is SqlTypeName.RowVersion or SqlTypeName.Timestamp) > 1)
        {
            throw new ArgumentException(message: "A table can contain only one rowversion column.", paramName: nameof(columns));
        }

        if (_columns.Count(predicate: column => column.Identity is not null) > 1)
        {
            throw new ArgumentException(message: "A table can contain only one identity column.", paramName: nameof(columns));
        }

        if (_columns.Count(predicate: column => column.IsRowGuidCol) > 1)
        {
            throw new ArgumentException(message: "A table can contain only one ROWGUIDCOL column.", paramName: nameof(columns));
        }

        if (_columns.Count(predicate: column => column.IsColumnSet) > 1)
        {
            throw new ArgumentException(message: "A table can contain only one XML column set.", paramName: nameof(columns));
        }

        var columnSet = _columns.SingleOrDefault(predicate: column => column.IsColumnSet);

        if (columnSet is not null && _columns.Any(predicate: column => column.IsSparse && column.MaskingFunction is not null))
        {
            throw new ArgumentException(message: "A SPARSE column participating in a column set cannot be dynamically masked.", paramName: nameof(columns));
        }

        if (columnSet is not null && _columns.Any(predicate: column => column.ComputedExpression is { } expression && ExpressionReferencesColumn(expression: expression, columnName: columnSet.Name)))
        {
            throw new ArgumentException(message: "Computed columns cannot reference an XML column set.", paramName: nameof(columns));
        }

        foreach (var masked in _columns.Where(predicate: column => column.MaskingFunction is not null))
        {
            if (_columns.Any(predicate: column => column.ComputedExpression is { } expression && ExpressionReferencesColumn(expression: expression, columnName: masked.Name)))
            {
                throw new ArgumentException(message: "A dynamically masked column cannot have a computed-column dependency.", paramName: nameof(columns));
            }
        }

        if (_columns.Any(predicate: column => column.IsFileStream) && !_columns.Any(predicate: column => column.IsRowGuidCol && !column.IsNullable))
        {
            throw new ArgumentException(message: "FILESTREAM requires a non-null ROWGUIDCOL column.", paramName: nameof(columns));
        }

        foreach (var vector in _columns.Where(predicate: column => column.Type.Name == SqlTypeName.Vector))
        {
            if (_checkConstraints.Any(predicate: check => ExpressionReferencesColumn(expression: check.Expression, columnName: vector.Name)))
            {
                throw new ArgumentException(message: "SQL Server vector columns do not support CHECK constraints.", paramName: nameof(checkConstraints));
            }
        }

        foreach (var column in _columns.Where(predicate: column => column.DefaultExpression is not null))
        {
            if (_columns.Any(predicate: candidate => ExpressionReferencesColumn(expression: column.DefaultExpression!, columnName: candidate.Name)))
            {
                throw new ArgumentException(message: $"DEFAULT expression for column '{column.Name}' cannot reference a table column.", paramName: nameof(columns));
            }
        }

        _ = GetComputedColumnOrder(columns: _columns);
        var identity = _columns.SingleOrDefault(predicate: column => column.Identity is not null)?.Identity;

        if (identity is null && nextIdentityValue is not null)
        {
            throw new ArgumentException(message: "A table without IDENTITY cannot have identity allocation state.", paramName: nameof(nextIdentityValue));
        }

        if (systemVersioning is not null)
        {
            systemVersioning = PrepareCatalogSystemVersioning(catalogSystemVersioning: systemVersioning);
            var start = _columns.SingleOrDefault(predicate: column => column.Id == systemVersioning.PeriodStartColumnId) ?? throw new ArgumentException(message: "The temporal period start column does not belong to the table.", paramName: nameof(systemVersioning));
            var end = _columns.SingleOrDefault(predicate: column => column.Id == systemVersioning.PeriodEndColumnId) ?? throw new ArgumentException(message: "The temporal period end column does not belong to the table.", paramName: nameof(systemVersioning));

            if (start.GeneratedAlways != CatalogGeneratedAlwaysKind.RowStart || end.GeneratedAlways != CatalogGeneratedAlwaysKind.RowEnd || start.Type.Name != SqlTypeName.DateTime2 || end.Type.Name != SqlTypeName.DateTime2 || start.Type != end.Type || start.IsNullable || end.IsNullable)
            {
                throw new ArgumentException(message: "A temporal period requires non-null datetime2 columns generated always as ROW START and ROW END.", paramName: nameof(systemVersioning));
            }
        }

        catalogTable.Id = id;
        catalogTable.Name = qualifiedName.Name;
        catalogTable.DatabaseName = qualifiedName.DatabaseName;
        catalogTable.SchemaName = qualifiedName.SchemaName;
        catalogTable.SchemaVersion = schemaVersion;
        catalogTable.FirstHeapPageId = firstHeapPageId;
        catalogTable.NextIdentityValue = identity is null ? null : nextIdentityValue ?? identity.Seed;
        catalogTable.SystemVersioning = systemVersioning;
        catalogTable.Graph = graph is null ? null : PrepareCatalogGraphTable(catalogGraphTable: graph);

        if (catalogTable.ObjectId == Guid.Empty)
        {
            catalogTable.ObjectId = Guid.NewGuid();
        }

        catalogTable.QualifiedName = CreateCatalogTableName(databaseName: catalogTable.DatabaseName, schemaName: catalogTable.SchemaName, name: catalogTable.Name);
        catalogTable.Columns = Array.AsReadOnly(array: _columns);
        catalogTable.CheckConstraints = Array.AsReadOnly(array: _checkConstraints);
    }

    private static void ValidateUnique<T>(IEnumerable<T> values, string description, string parameterName, IEqualityComparer<T>? comparer = null)
    {
        var materialized = values.ToArray();

        if (materialized.Distinct(comparer: comparer)
            .Count() != materialized.Length)
        {
            throw new ArgumentException(message: $"{description} must be unique within their scope.", paramName: parameterName);
        }
    }

    private static bool ExpressionReferencesColumn(string expression, string columnName)
    {
        ArgumentNullException.ThrowIfNull(argument: expression);
        ArgumentNullException.ThrowIfNull(argument: columnName);

        foreach (var identifier in ExpressionIdentifiers(expression: expression))
        {
            if (identifier.Equals(value: columnName, comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<int> GetComputedColumnOrder(IReadOnlyList<CatalogColumn> columns)
    {
        var computed = columns.Select(selector: (column, position) => (column, position))
            .Where(predicate: item => item.column.IsComputed)
            .ToArray();

        if (computed.Length == 0)
        {
            return[];
        }

        var computedNames = computed.Select(selector: item => item.column.Name)
            .ToHashSet(comparer: StringComparer.OrdinalIgnoreCase);

        var dependencies = computed.ToDictionary(keySelector: item => item.position, elementSelector: item => ExpressionIdentifiers(expression: item.column.ComputedExpression!)
            .Where(predicate: identifier => computedNames.Contains(item: identifier))
            .ToHashSet(comparer: StringComparer.OrdinalIgnoreCase));

        var completed = new HashSet<string>(comparer: StringComparer.OrdinalIgnoreCase);
        var order = new List<int>(capacity: computed.Length);

        while (order.Count != computed.Length)
        {
            var progressed = false;

            foreach (var item in computed.Where(predicate: item => !order.Contains(item: item.position)))
            {
                if (!dependencies[item.position].IsSubsetOf(other: completed))
                {
                    continue;
                }

                order.Add(item: item.position);
                completed.Add(item: item.column.Name);
                progressed = true;
            }

            if (!progressed)
            {
                throw new ArgumentException(message: "Computed-column dependencies contain a cycle.", paramName: nameof(columns));
            }
        }

        return order;
    }

    private static IEnumerable<string> ExpressionIdentifiers(string expression)
    {
        ArgumentNullException.ThrowIfNull(argument: expression);

        for (var position = 0; position < expression.Length;)
        {
            if (expression[position] == '\'')
            {
                position++;

                while (position < expression.Length)
                {
                    if (expression[position++] != '\'')
                    {
                        continue;
                    }

                    if (position < expression.Length && expression[position] == '\'')
                    {
                        position++;
                        continue;
                    }

                    break;
                }

                continue;
            }

            if (expression[position] == '[')
            {
                var identifier = new System.Text.StringBuilder();
                position++;

                while (position < expression.Length)
                {
                    if (expression[position] != ']')
                    {
                        identifier.Append(value: expression[position++]);
                        continue;
                    }

                    position++;

                    if (position < expression.Length && expression[position] == ']')
                    {
                        identifier.Append(value: ']');
                        position++;
                        continue;
                    }

                    break;
                }

                if (identifier.Length != 0)
                {
                    yield return identifier.ToString();
                }

                continue;
            }

            if (!char.IsLetter(c: expression[position]) && expression[position] is not ('_' or '@'))
            {
                position++;
                continue;
            }

            var start = position++;

            while (position < expression.Length && (char.IsLetterOrDigit(c: expression[position]) || expression[position] is '_' or '@' or '$'))
            {
                position++;
            }

            var token = expression[start..position];
            var next = position;

            while (next < expression.Length && char.IsWhiteSpace(c: expression[next]))
            {
                next++;
            }

            if (next < expression.Length && expression[next] == '(' || token.Equals(value: "CURRENT_TIMESTAMP", comparisonType: StringComparison.OrdinalIgnoreCase) || token.Equals(value: "NULL", comparisonType: StringComparison.OrdinalIgnoreCase) || token.Equals(value: "AND", comparisonType: StringComparison.OrdinalIgnoreCase) || token.Equals(value: "OR", comparisonType: StringComparison.OrdinalIgnoreCase) || token.Equals(value: "NOT", comparisonType: StringComparison.OrdinalIgnoreCase) || token.Equals(value: "IS", comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return token;
        }
    }

    private static void ValidateCatalogCheckConstraint(CatalogCheckConstraint catalogCheckConstraint)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogCheckConstraint);
        var name = catalogCheckConstraint.Name;
        var expression = catalogCheckConstraint.Expression;
        ValidateIdentifier(value: name, parameterName: nameof(name));

        if (string.IsNullOrWhiteSpace(value: expression))
        {
            throw new ArgumentException(message: "Check expression cannot be empty.", paramName: nameof(expression));
        }
    }

    private static void ValidateIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value: value))
        {
            throw new ArgumentException(message: "SQL identifier cannot be empty.", paramName: parameterName);
        }

        if (value.Length > 128)
        {
            throw new ArgumentOutOfRangeException(paramName: parameterName, message: "SQL identifiers cannot exceed 128 characters.");
        }
    }

    private static CatalogCheckConstraint PrepareCatalogCheckConstraint(CatalogCheckConstraint catalogCheckConstraint)
    {
        ValidateCatalogCheckConstraint(catalogCheckConstraint: catalogCheckConstraint);
        return catalogCheckConstraint;
    }

    private static void ValidateCatalogGraphTable(CatalogGraphTable catalogGraphTable)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogGraphTable);
        var kind = catalogGraphTable.Kind;
        var identityColumnId = catalogGraphTable.IdentityColumnId;
        var identityIndexId = catalogGraphTable.IdentityIndexId;
        var fromNodeTableId = catalogGraphTable.FromNodeTableId;
        var toNodeTableId = catalogGraphTable.ToNodeTableId;
        var fromNodeColumnId = catalogGraphTable.FromNodeColumnId;
        var toNodeColumnId = catalogGraphTable.ToNodeColumnId;
        var outgoingIndexId = catalogGraphTable.OutgoingIndexId;
        var incomingIndexId = catalogGraphTable.IncomingIndexId;

        if (!Enum.IsDefined(value: kind))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(kind));
        }

        if (kind == GraphTableKind.Node && new object? []
        {
            fromNodeTableId,
            toNodeTableId,
            fromNodeColumnId,
            toNodeColumnId,
            outgoingIndexId,
            incomingIndexId
        }.Any(predicate: value => value is not null))
        {
            throw new ArgumentException(message: "A graph node table cannot declare edge endpoint metadata.");
        }

        if (kind == GraphTableKind.Edge && new object? []
        {
            fromNodeTableId,
            toNodeTableId,
            fromNodeColumnId,
            toNodeColumnId,
            outgoingIndexId,
            incomingIndexId
        }.Any(predicate: value => value is null))
        {
            throw new ArgumentException(message: "A graph edge table requires complete endpoint and adjacency metadata.");
        }

        if (kind == GraphTableKind.Edge && (identityColumnId == fromNodeColumnId || identityColumnId == toNodeColumnId || fromNodeColumnId == toNodeColumnId))
        {
            throw new ArgumentException(message: "Graph identity and endpoint columns must be distinct.");
        }

        if (kind == GraphTableKind.Edge && (identityIndexId == outgoingIndexId || identityIndexId == incomingIndexId || outgoingIndexId == incomingIndexId))
        {
            throw new ArgumentException(message: "Graph identity and adjacency indexes must be distinct.");
        }
    }

    private static void ValidateCatalogSystemVersioning(CatalogSystemVersioning catalogSystemVersioning)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogSystemVersioning);
        var historyTableId = catalogSystemVersioning.HistoryTableId;
        var periodStartColumnId = catalogSystemVersioning.PeriodStartColumnId;
        var periodEndColumnId = catalogSystemVersioning.PeriodEndColumnId;

        if (periodStartColumnId == periodEndColumnId)
        {
            throw new ArgumentException(message: "The temporal period start and end columns must be different.");
        }
    }

    private static CatalogGraphTable PrepareCatalogGraphTable(CatalogGraphTable catalogGraphTable)
    {
        ValidateCatalogGraphTable(catalogGraphTable: catalogGraphTable);
        return catalogGraphTable;
    }

    private static CatalogSystemVersioning PrepareCatalogSystemVersioning(CatalogSystemVersioning catalogSystemVersioning)
    {
        ValidateCatalogSystemVersioning(catalogSystemVersioning: catalogSystemVersioning);
        return catalogSystemVersioning;
    }

    private static CatalogTableName CreateCatalogTableName(string databaseName, string schemaName, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: databaseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: schemaName);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: name);

        return new CatalogTableName
        {
            DatabaseName = databaseName,
            SchemaName = schemaName,
            Name = name
        };
    }
}