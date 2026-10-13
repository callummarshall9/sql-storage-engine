// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System.Numerics;
using System.Collections.Generic;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Services.Processings.CatalogMetadata;

namespace sql_storage_engine.Exposures.Catalog;
public static class CatalogTableExposure
{
    private static readonly ICatalogTableProcessingService service = new CatalogTableProcessingService();
    public static CatalogTable CreateCatalogTable(TableId tableId, string name, ulong schemaVersion, PageId pageId, IEnumerable<CatalogColumn> columns, IEnumerable<CatalogCheckConstraint>? checkConstraints = null, BigInteger? nextIdentityValue = null, string databaseName = "default", string schemaName = "dbo", CatalogSystemVersioning? catalogSystemVersioning = null, CatalogGraphTable? catalogGraphTable = null) =>
        service.PrepareCatalogTable(catalogTable: new CatalogTable { IdSource = tableId, NameSource = name, SchemaVersionSource = schemaVersion, FirstHeapPageIdSource = pageId, ColumnsSource = columns, CheckConstraintsSource = checkConstraints, NextIdentityValueSource = nextIdentityValue, DatabaseNameSource = databaseName, SchemaNameSource = schemaName, SystemVersioningSource = catalogSystemVersioning, GraphSource = catalogGraphTable });

    public static CatalogTable PrepareCatalogTable(CatalogTable catalogTable) =>
        service.PrepareCatalogTable(catalogTable: catalogTable);
}