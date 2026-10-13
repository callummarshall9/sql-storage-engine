// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Foundations.CatalogNames;
internal interface ICatalogTableNameService
{
    CatalogTableName PrepareCatalogTableName(CatalogTableName catalogTableName);

    string FormatCatalogTableName(CatalogTableName catalogTableName);
}