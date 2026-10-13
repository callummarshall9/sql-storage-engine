// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal sealed partial class CatalogIndexedColumnProcessingService
{
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
}