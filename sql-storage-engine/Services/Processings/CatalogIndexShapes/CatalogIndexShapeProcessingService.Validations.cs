// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogIndexShapes;
internal sealed partial class CatalogIndexShapeProcessingService
{
    private static void ValidateCatalogIndex(CatalogIndex catalogIndex)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogIndex);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: catalogIndex.Name);
        ArgumentNullException.ThrowIfNull(argument: catalogIndex.Columns);
        ArgumentNullException.ThrowIfNull(argument: catalogIndex.IncludedColumns);

        if (catalogIndex.Columns.Count == 0 || catalogIndex.Columns.Any(predicate: item => item is null))
        {
            throw new ArgumentException(message: "An index must define non-null columns.", paramName: "columns");
        }

        if (!Enum.IsDefined(value: catalogIndex.StorageKind) || catalogIndex.StorageKind == CatalogIndexStorageKind.Hash)
        {
            throw new ArgumentOutOfRangeException(paramName: "storageKind", message: "Disk tables support clustered or nonclustered indexes.");
        }

        if (catalogIndex.Columns.Select(selector: item => item.ColumnId)
            .Distinct()
            .Count() != catalogIndex.Columns.Count)
        {
            throw new ArgumentException(message: "Indexed column IDs must be unique.", paramName: "columns");
        }

        if (catalogIndex.IncludedColumns.Distinct()
            .Count() != catalogIndex.IncludedColumns.Count)
        {
            throw new ArgumentException(message: "Included column IDs must be unique.", paramName: "includedColumns");
        }

        if (catalogIndex.IncludedColumns.Intersect(second: catalogIndex.Columns.Select(selector: item => item.ColumnId))
            .Any())
        {
            throw new ArgumentException(message: "An included column cannot also be a key column.", paramName: "includedColumns");
        }

        if (catalogIndex.IsPrimaryKey && !catalogIndex.IsUnique)
        {
            throw new ArgumentException(message: "A primary key must be unique.", paramName: "isUnique");
        }

        if (catalogIndex.IgnoreDuplicateKey && !catalogIndex.IsUnique)
        {
            throw new ArgumentException(message: "IGNORE_DUP_KEY applies only to unique indexes.", paramName: "ignoreDuplicateKey");
        }

        if (catalogIndex.IncludedColumns.Count != 0 && (catalogIndex.StorageKind != CatalogIndexStorageKind.NonClustered || catalogIndex.IsPrimaryKey))
        {
            throw new ArgumentException(message: "Included columns require a nonclustered index without a primary-key constraint.", paramName: "includedColumns");
        }

        if (catalogIndex.SpecializedOptions is not null && (catalogIndex.Columns.Count != 1 || catalogIndex.IsUnique || catalogIndex.StorageKind != CatalogIndexStorageKind.NonClustered || catalogIndex.IsPrimaryKey || catalogIndex.IncludedColumns.Count != 0))
        {
            throw new ArgumentException(message: "Specialized indexes require one nonunique nonclustered source column without a primary key or included columns.", paramName: "specializedOptions");
        }
    }
}