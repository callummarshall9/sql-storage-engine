// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal sealed partial class SqlCollationCatalogColumnProcessingService : ISqlCollationCatalogColumnProcessingService
{
    private readonly sql_storage_engine.Services.Foundations.SqlCollations.SqlCollationService collations;
    public SqlCollationCatalogColumnProcessingService(sql_storage_engine.Services.Foundations.SqlCollations.SqlCollationService collations)
    {
        this.collations = collations;
    }

    public CatalogColumn PrepareCatalogColumn(CatalogColumn catalogColumn) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogColumn(catalogColumn: catalogColumn);
        return catalogColumn;
    });

    internal static SqlCollationCatalogColumnProcessingService CreateSqlCollationCatalogColumnProcessingService() =>
        new SqlCollationCatalogColumnProcessingService(collations: new sql_storage_engine.Services.Foundations.SqlCollations.SqlCollationService());
}