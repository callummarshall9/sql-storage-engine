// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.CatalogMasks;
internal interface ISqlMaskFunctionProcessingService
{
    SqlMaskContext ReadSqlMaskContext(SqlMaskContext sqlMaskContext);
}