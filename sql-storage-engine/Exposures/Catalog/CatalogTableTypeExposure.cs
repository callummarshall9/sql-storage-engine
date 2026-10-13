// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Services.Processings.CatalogMetadata;

namespace sql_storage_engine.Exposures.Catalog;
public static class CatalogTableTypeExposure
{
    private static readonly ICatalogTableTypeProcessingService service = new CatalogTableTypeProcessingService();
    public static CatalogTableType CreateCatalogTableType(string schemaName, string name, IEnumerable<CatalogColumn> columns, IEnumerable<CatalogTableTypeIndex>? indexes = null, IEnumerable<CatalogCheckConstraint>? checkConstraints = null, bool isMemoryOptimized = false) =>
        service.PrepareCatalogTableType(catalogTableType: new CatalogTableType { SchemaNameSource = schemaName, NameSource = name, ColumnsSource = columns, IndexesSource = indexes, CheckConstraintsSource = checkConstraints, IsMemoryOptimizedSource = isMemoryOptimized });

    public static CatalogTableType PrepareCatalogTableType(CatalogTableType catalogTableType) =>
        service.PrepareCatalogTableType(catalogTableType: catalogTableType);
}