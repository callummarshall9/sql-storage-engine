// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal interface ICatalogIndexKeySizeProcessingService
{
    int CalculateMaximumSqlTypeIndexKeyBytes(SqlType sqlType);

    int CalculateMinimumSqlTypeIndexKeyBytes(SqlType sqlType);
}