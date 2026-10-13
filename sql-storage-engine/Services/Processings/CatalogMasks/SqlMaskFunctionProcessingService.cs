// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.CatalogMasks;
internal sealed partial class SqlMaskFunctionProcessingService : ISqlMaskFunctionProcessingService
{
    private readonly sql_storage_engine.Services.Foundations.SqlMaskFunctions.SqlMaskFunctionService sqlMaskFunctionService = new sql_storage_engine.Services.Foundations.SqlMaskFunctions.SqlMaskFunctionService();
    public SqlMaskContext ReadSqlMaskContext(SqlMaskContext sqlMaskContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlMaskContext(sqlMaskContext: sqlMaskContext);
        return sqlMaskFunctionService.ReadSqlMaskContext(sqlMaskContext: sqlMaskContext);
    });
}