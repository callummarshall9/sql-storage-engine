// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Foundations.CatalogNames;
internal sealed partial class CatalogTableNameService : ICatalogTableNameService
{
    public CatalogTableName PrepareCatalogTableName(CatalogTableName catalogTableName) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogTableName(catalogTableName: catalogTableName);
        return catalogTableName;
    });

    public string FormatCatalogTableName(CatalogTableName catalogTableName) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogTableName(catalogTableName: catalogTableName);
        return $"[{catalogTableName.DatabaseName}].[{catalogTableName.SchemaName}].[{catalogTableName.Name}]";
    });
}