// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Services.Processings.CatalogMetadata;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Exposures.Catalog;
public static class CatalogIndexKeySizeExposure
{
    private static readonly ICatalogIndexKeySizeProcessingService service = new CatalogIndexKeySizeProcessingService();
    public static int CalculateMaximumIndexKeyBytes(SqlType sqlType) =>
        service.CalculateMaximumSqlTypeIndexKeyBytes(sqlType: sqlType);

    public static int CalculateMinimumIndexKeyBytes(SqlType sqlType) =>
        service.CalculateMinimumSqlTypeIndexKeyBytes(sqlType: sqlType);
}