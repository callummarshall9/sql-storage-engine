// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Services.Foundations.CatalogNames;

namespace sql_storage_engine.Exposures.Catalog;
public static class CatalogTableNameExposure
{
    private static readonly ICatalogTableNameService service = new CatalogTableNameService();
    public static CatalogTableName CreateCatalogTableName(string databaseName, string schemaName, string name) =>
        service.PrepareCatalogTableName(catalogTableName: new CatalogTableName { DatabaseName = databaseName, SchemaName = schemaName, Name = name });

    public static string FormatCatalogTableName(CatalogTableName catalogTableName) =>
        service.FormatCatalogTableName(catalogTableName: catalogTableName);
}