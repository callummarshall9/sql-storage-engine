// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Foundations.CatalogNames;
internal sealed partial class CatalogTableNameService
{
    private static void ValidateCatalogTableName(CatalogTableName catalogTableName)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogTableName);
        var databaseName = catalogTableName.DatabaseName;
        var schemaName = catalogTableName.SchemaName;
        var name = catalogTableName.Name;
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: databaseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: schemaName);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: name);
    }
}