// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System;
using sql_storage_engine.Catalog;
using System.Collections.Generic;
using System.Linq;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal sealed partial class CatalogTableTypeIndexProcessingService
{
    private static void ValidateCatalogTableTypeIndex(CatalogTableTypeIndex catalogTableTypeIndex)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogTableTypeIndex);
        var name = catalogTableTypeIndex.NameSource;
        var isPrimaryKey = catalogTableTypeIndex.IsPrimaryKeySource;
        var isUnique = catalogTableTypeIndex.IsUniqueSource;
        var columns = catalogTableTypeIndex.ColumnsSource;
        var storageKind = catalogTableTypeIndex.StorageKindSource;
        var includedColumns = catalogTableTypeIndex.IncludedColumnsSource;
        var ignoreDuplicateKey = catalogTableTypeIndex.IgnoreDuplicateKeySource;
        var bucketCount = catalogTableTypeIndex.BucketCountSource;
        CatalogIndexedColumn[] _columns;
        ColumnId[] _includedColumns;
        name = CheckIdentifier(value: name, parameterName: nameof(name));
        ArgumentNullException.ThrowIfNull(argument: columns);

        _columns = columns.Select(selector: PrepareCatalogIndexedColumn)
            .ToArray();

        if (_columns.Length == 0)
        {
            throw new ArgumentException(message: "A table-type index must define at least one column.", paramName: nameof(columns));
        }

        ValidateUnique(values: _columns.Select(selector: column => column.ColumnId), description: "Indexed column IDs", parameterName: nameof(columns));

        if (isPrimaryKey && !isUnique)
        {
            throw new ArgumentException(message: "A primary key must be unique.", paramName: nameof(isUnique));
        }

        if (!Enum.IsDefined(value: storageKind))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(storageKind));
        }

        _includedColumns = includedColumns?.ToArray() ?? [];
        ValidateUnique(values: _includedColumns, description: "Included column IDs", parameterName: nameof(includedColumns));

        if (_includedColumns.Intersect(second: _columns.Select(selector: column => column.ColumnId))
            .Any())
        {
            throw new ArgumentException(message: "An included column cannot also be a key column.", paramName: nameof(includedColumns));
        }

        if (ignoreDuplicateKey && !isUnique)
        {
            throw new ArgumentException(message: "IGNORE_DUP_KEY applies only to unique indexes.", paramName: nameof(ignoreDuplicateKey));
        }

        if (_includedColumns.Length != 0 && storageKind != CatalogIndexStorageKind.NonClustered)
        {
            throw new ArgumentException(message: "Included columns apply only to nonclustered indexes.", paramName: nameof(includedColumns));
        }

        if (isPrimaryKey && _includedColumns.Length != 0)
        {
            throw new ArgumentException(message: "A primary-key constraint cannot declare included columns.", paramName: nameof(includedColumns));
        }

        if (storageKind == CatalogIndexStorageKind.Hash)
        {
            if (bucketCount is null or <= 0 or > 1_073_741_824)
            {
                throw new ArgumentOutOfRangeException(paramName: nameof(bucketCount), message: "A hash index requires BUCKET_COUNT from 1 through 1,073,741,824.");
            }

            if (includedColumns is not null && _includedColumns.Length != 0)
            {
                throw new ArgumentException(message: "Hash indexes cannot define included columns.", paramName: nameof(includedColumns));
            }
        }
        else if (bucketCount is not null)
        {
            throw new ArgumentException(message: "BUCKET_COUNT applies only to hash indexes.", paramName: nameof(bucketCount));
        }

        catalogTableTypeIndex.Name = name;
        catalogTableTypeIndex.IsPrimaryKey = isPrimaryKey;
        catalogTableTypeIndex.IsUnique = isUnique;
        catalogTableTypeIndex.StorageKind = storageKind;
        catalogTableTypeIndex.IgnoreDuplicateKey = ignoreDuplicateKey;
        catalogTableTypeIndex.BucketCount = bucketCount;
        catalogTableTypeIndex.Columns = Array.AsReadOnly(array: _columns);
        catalogTableTypeIndex.IncludedColumns = Array.AsReadOnly(array: _includedColumns);
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

    private static void ValidateCatalogIndexedColumn(CatalogIndexedColumn catalogIndexedColumn)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogIndexedColumn);
        var columnId = catalogIndexedColumn.ColumnId;
        var direction = catalogIndexedColumn.Direction;
        var nullSortOrder = catalogIndexedColumn.NullSortOrder;
        var collation = catalogIndexedColumn.Collation;

        if (!Enum.IsDefined(value: direction))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(direction));
        }

        if (!Enum.IsDefined(value: nullSortOrder))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(nullSortOrder));
        }

        if (collation is not null && string.IsNullOrWhiteSpace(value: collation))
        {
            throw new ArgumentException(message: "Collation cannot be empty when specified.", paramName: nameof(collation));
        }
    }

    private static CatalogIndexedColumn PrepareCatalogIndexedColumn(CatalogIndexedColumn catalogIndexedColumn)
    {
        ValidateCatalogIndexedColumn(catalogIndexedColumn: catalogIndexedColumn);
        return catalogIndexedColumn;
    }

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
}