// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Rows;
using System.Numerics;
using System;
using sql_storage_engine.Catalog;
using System.Collections.Generic;
using System.Linq;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal sealed partial class CatalogTableTypeProcessingService
{
    private static void ValidateCatalogTableType(CatalogTableType catalogTableType)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogTableType);
        var schemaName = catalogTableType.SchemaNameSource;
        var name = catalogTableType.NameSource;
        var columns = catalogTableType.ColumnsSource;
        var indexes = catalogTableType.IndexesSource;
        var checkConstraints = catalogTableType.CheckConstraintsSource;
        var isMemoryOptimized = catalogTableType.IsMemoryOptimizedSource;
        CatalogColumn[] _columns;
        CatalogTableTypeIndex[] _indexes;
        CatalogCheckConstraint[] _checkConstraints;
        catalogTableType.SchemaName = CheckIdentifier(value: schemaName, parameterName: nameof(schemaName));
        catalogTableType.Name = CheckIdentifier(value: name, parameterName: nameof(name));
        ArgumentNullException.ThrowIfNull(argument: columns);
        _columns = columns.ToArray();
        _indexes = indexes?.ToArray() ?? [];

        _checkConstraints = checkConstraints?.Select(selector: PrepareCatalogCheckConstraint)
            .ToArray() ?? [];

        if (_columns.Length == 0)
        {
            throw new ArgumentException(message: "A table type must define at least one column.", paramName: nameof(columns));
        }

        if (_columns.Length > 1_024)
        {
            throw new ArgumentException(message: "A user-defined table type cannot contain more than 1,024 columns.", paramName: nameof(columns));
        }

        ValidateUnique(values: _columns.Select(selector: column => column.Id), description: "Column IDs", parameterName: nameof(columns));
        ValidateUnique(values: _columns.Select(selector: column => column.Name), description: "Column names", parameterName: nameof(columns), comparer: StringComparer.Ordinal);
        ValidateUnique(values: _indexes.Select(selector: index => index.Name), description: "Table-type index names", parameterName: nameof(indexes), comparer: StringComparer.Ordinal);
        ValidateUnique(values: _checkConstraints.Select(selector: check => check.Name), description: "Table-type check-constraint names", parameterName: nameof(checkConstraints), comparer: StringComparer.Ordinal);

        if (_columns.Count(predicate: column => column.Identity is not null) > 1)
        {
            throw new ArgumentException(message: "A table type can contain only one identity column.", paramName: nameof(columns));
        }

        if (_columns.Count(predicate: column => column.Type.Name is SqlTypeName.RowVersion or SqlTypeName.Timestamp) > 1)
        {
            throw new ArgumentException(message: "A table type can contain only one rowversion column.", paramName: nameof(columns));
        }

        if (_columns.Count(predicate: column => column.IsRowGuidCol) > 1)
        {
            throw new ArgumentException(message: "A table type can contain only one ROWGUIDCOL column.", paramName: nameof(columns));
        }

        if (_columns.Any(predicate: column => column.IsSparse || column.IsColumnSet))
        {
            throw new ArgumentException(message: "User-defined table types cannot contain SPARSE columns or a column set.", paramName: nameof(columns));
        }

        if (_columns.Any(predicate: column => column.IsFileStream || column.MaskingFunction is not null || column.Encryption is not null || column.GeneratedAlways != CatalogGeneratedAlwaysKind.None || column.IsHidden))
        {
            throw new ArgumentException(message: "User-defined table types cannot contain FILESTREAM, masking, encryption, or generated-always column facets.", paramName: nameof(columns));
        }

        foreach (var column in _columns.Where(predicate: column => column.DefaultExpression is not null))
        {
            if (_columns.Any(predicate: candidate => ExpressionReferencesColumn(expression: column.DefaultExpression!, columnName: candidate.Name)))
            {
                throw new ArgumentException(message: $"DEFAULT expression for column '{column.Name}' cannot reference a table-type column.", paramName: nameof(columns));
            }
        }

        _ = GetComputedColumnOrder(columns: _columns);

        if (isMemoryOptimized && _columns.Any(predicate: column => IsUserDefined(type: column.Type) || column.Type.Name is SqlTypeName.DateTimeOffset or SqlTypeName.Geography or SqlTypeName.Geometry or SqlTypeName.HierarchyId or SqlTypeName.Json or SqlTypeName.RowVersion or SqlTypeName.Timestamp or SqlTypeName.SqlVariant or SqlTypeName.Vector or SqlTypeName.Xml or SqlTypeName.Text or SqlTypeName.NText or SqlTypeName.Image))
        {
            throw new ArgumentException(message: "A memory-optimized table type contains an unsupported SQL data type.", paramName: nameof(columns));
        }

        if (isMemoryOptimized && _columns.Any(predicate: column => column.Identity is { } identity && (identity.Seed != BigInteger.One || identity.Increment != BigInteger.One)))
        {
            throw new ArgumentException(message: "Memory-optimized table types support only IDENTITY(1,1).", paramName: nameof(columns));
        }

        if (isMemoryOptimized && (_columns.Any(predicate: column => column.DefaultExpression is not null || column.IsComputed || column.IsRowGuidCol) || _checkConstraints.Length != 0))
        {
            throw new ArgumentException(message: "Memory-optimized table types do not support DEFAULT, computed, ROWGUIDCOL, or CHECK metadata.", paramName: nameof(columns));
        }

        if (isMemoryOptimized && _indexes.Length == 0)
        {
            throw new ArgumentException(message: "A memory-optimized table type requires at least one index.", paramName: nameof(indexes));
        }

        if (_indexes.Count(predicate: index => index.IsPrimaryKey) > 1)
        {
            throw new ArgumentException(message: "A table type can define only one primary key.", paramName: nameof(indexes));
        }

        var columnIds = _columns.Select(selector: column => column.Id)
            .ToHashSet();

        foreach (var index in _indexes)
        {
            if (index.Columns.Any(predicate: column => !columnIds.Contains(item: column.ColumnId)))
            {
                throw new ArgumentException(message: $"Table-type index '{index.Name}' references an unknown column.", paramName: nameof(indexes));
            }

            foreach (var indexedColumn in index.Columns)
            {
                var column = _columns.Single(predicate: candidate => candidate.Id == indexedColumn.ColumnId);

                if (!CanBeBTreeKey(type: column.Type))
                {
                    throw new ArgumentException(message: $"SQL Server type {FormatType(type: column.Type)} cannot be a table-type index key.", paramName: nameof(indexes));
                }

                if (index.IsPrimaryKey && column.IsNullable)
                {
                    throw new ArgumentException(message: "Primary-key table-type columns cannot be nullable.", paramName: nameof(indexes));
                }
            }

            if (index.IncludedColumns.Any(predicate: column => !columnIds.Contains(item: column)))
            {
                throw new ArgumentException(message: $"Table-type index '{index.Name}' includes an unknown column.", paramName: nameof(indexes));
            }

            if (index.Columns.Count > 32)
            {
                throw new ArgumentException(message: "A table-type index cannot contain more than 32 key columns.", paramName: nameof(indexes));
            }

            if (index.IncludedColumns.Count > 1_023)
            {
                throw new ArgumentException(message: "A table-type index cannot contain more than 1,023 included columns.", paramName: nameof(indexes));
            }

            foreach (var includedId in index.IncludedColumns)
            {
                var included = _columns.Single(predicate: column => column.Id == includedId);

                if (included.Type.Name is SqlTypeName.Text or SqlTypeName.NText or SqlTypeName.Image || included.IsColumnSet)
                {
                    throw new ArgumentException(message: $"Column '{included.Name}' cannot be included in an index.", paramName: nameof(indexes));
                }
            }

            if (index.StorageKind == CatalogIndexStorageKind.Hash && !isMemoryOptimized)
            {
                throw new ArgumentException(message: "Hash indexes require a memory-optimized table type.", paramName: nameof(indexes));
            }

            if (isMemoryOptimized && index.StorageKind == CatalogIndexStorageKind.Clustered)
            {
                throw new ArgumentException(message: "Memory-optimized table types support only nonclustered or hash indexes.", paramName: nameof(indexes));
            }

            if (isMemoryOptimized && (index.IncludedColumns.Count != 0 || index.IgnoreDuplicateKey || index.IsUnique && !index.IsPrimaryKey))
            {
                throw new ArgumentException(message: "Memory-optimized table-type indexes do not support INCLUDE, IGNORE_DUP_KEY, or standalone UNIQUE metadata.", paramName: nameof(indexes));
            }

            if (isMemoryOptimized && index.StorageKind == CatalogIndexStorageKind.NonClustered && index.Columns.Sum(selector: indexed => MaximumIndexKeyBytes(type: _columns.Single(predicate: column => column.Id == indexed.ColumnId)
            .Type)) > 2_500)
            {
                throw new ArgumentException(message: "A memory-optimized nonclustered table-type index cannot exceed 2,500 declared key bytes.", paramName: nameof(indexes));
            }

            if (!isMemoryOptimized)
            {
                var maximum = index.StorageKind == CatalogIndexStorageKind.Clustered ? sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.MaximumClusteredKeyBytes : sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.MaximumNonClusteredKeyBytes;

                if (index.Columns.Sum(selector: indexed => MinimumIndexKeyBytes(type: _columns.Single(predicate: column => column.Id == indexed.ColumnId)
            .Type)) > maximum)
                {
                    throw new ArgumentException(message: "The fixed-width table-type index key exceeds its SQL Server limit.", paramName: nameof(indexes));
                }
            }
        }

        catalogTableType.IsMemoryOptimized = isMemoryOptimized;
        catalogTableType.QualifiedName = $"[{catalogTableType.SchemaName}].[{catalogTableType.Name}]";
        catalogTableType.Columns = Array.AsReadOnly(array: _columns);
        catalogTableType.Indexes = Array.AsReadOnly(array: _indexes);
        catalogTableType.CheckConstraints = Array.AsReadOnly(array: _checkConstraints);
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

        var dependencies = computed.ToDictionary(keySelector: item => item.position, elementSelector: item => computedNames.Where(predicate: name => ExpressionReferencesColumn(expression: item.column.ComputedExpression!, columnName: name))
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

    private static bool IsUserDefined(SqlType type) =>
        type.UserTypeKind != SqlUserTypeKind.None;

    private static bool CanBeBTreeKey(SqlType type) =>
        type.UserTypeKind == SqlUserTypeKind.Clr ? type.ClrIsByteOrdered : type.Name switch
    {
        SqlTypeName.Text or SqlTypeName.NText or SqlTypeName.Image or SqlTypeName.Xml or SqlTypeName.Json or SqlTypeName.Geometry or SqlTypeName.Geography or SqlTypeName.Vector => false,
        SqlTypeName.VarChar or SqlTypeName.NVarChar or SqlTypeName.VarBinary when type.IsMax => false,
        SqlTypeName.Cursor or SqlTypeName.Table => false,
        _ => true
    };

    private static string FormatType(SqlType type) =>
        type.UserTypeKind != SqlUserTypeKind.None ? $"[{type.UserTypeSchema}].[{type.UserTypeName}]" : type.Name.ToString();

    private static string CheckIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value: value))
        {
            throw new ArgumentException(message: "SQL identifier cannot be empty.", paramName: parameterName);
        }

        if (value.Length > 128)
        {
            throw new ArgumentOutOfRangeException(paramName: parameterName, message: "SQL identifiers cannot exceed 128 characters.");
        }

        return value;
    }

    private static int MaximumIndexKeyBytes(SqlType type) =>
        type.UserTypeKind switch
    {
        SqlUserTypeKind.Alias => MaximumIndexKeyBytes(type: type.BaseType!),
        SqlUserTypeKind.Clr when type.ClrMaxByteSize is null => sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.MaximumNonClusteredKeyBytes,
        SqlUserTypeKind.Clr => type.ClrMaxByteSize is> 0 and <= sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.MaximumNonClusteredKeyBytes ? type.ClrMaxByteSize.Value : sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.MaximumNonClusteredKeyBytes + 1,
        _ => type.Name switch
        {
            SqlTypeName.Bit or SqlTypeName.TinyInt => 1,
            SqlTypeName.SmallInt => 2,
            SqlTypeName.Int or SqlTypeName.Real or SqlTypeName.SmallMoney or SqlTypeName.SmallDateTime => 4,
            SqlTypeName.BigInt or SqlTypeName.Money or SqlTypeName.DateTime => 8,
            SqlTypeName.Float => type.Precision <= 24 ? 4 : 8,
            SqlTypeName.Date => 3,
            SqlTypeName.Time => type.Scale <= 2 ? 3 : type.Scale <= 4 ? 4 : 5,
            SqlTypeName.DateTime2 => type.Scale <= 2 ? 6 : type.Scale <= 4 ? 7 : 8,
            SqlTypeName.DateTimeOffset => type.Scale <= 2 ? 8 : type.Scale <= 4 ? 9 : 10,
            SqlTypeName.Decimal or SqlTypeName.Numeric => type.Precision <= 9 ? 5 : type.Precision <= 19 ? 9 : type.Precision <= 28 ? 13 : 17,
            SqlTypeName.UniqueIdentifier => 16,
            SqlTypeName.RowVersion or SqlTypeName.Timestamp => 8,
            SqlTypeName.Char or SqlTypeName.VarChar or SqlTypeName.Binary or SqlTypeName.VarBinary => type.Length!.Value,
            SqlTypeName.NChar or SqlTypeName.NVarChar => checked(type.Length!.Value * 2),
            SqlTypeName.HierarchyId => 892,
            SqlTypeName.SqlVariant => sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.MaximumSqlVariantKeyBytes,
            _ => sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.MaximumNonClusteredKeyBytes + 1
        }};

    private static int MinimumIndexKeyBytes(SqlType type) =>
        type.UserTypeKind switch
    {
        SqlUserTypeKind.Alias => MinimumIndexKeyBytes(type: type.BaseType!),
        SqlUserTypeKind.Clr => type.ClrIsFixedLength && type.ClrMaxByteSize is> 0 ? type.ClrMaxByteSize.Value : 0,
        _ => type.Name switch
        {
            SqlTypeName.VarChar or SqlTypeName.NVarChar or SqlTypeName.VarBinary or SqlTypeName.HierarchyId or SqlTypeName.SqlVariant => 0,
            _ => MaximumIndexKeyBytes(type: type)}};
}