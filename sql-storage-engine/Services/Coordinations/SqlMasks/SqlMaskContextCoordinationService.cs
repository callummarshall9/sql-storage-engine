// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Coordinations.SqlMasks;
internal sealed partial class SqlMaskContextCoordinationService : ISqlMaskContextCoordinationService
{
    private readonly sql_storage_engine.Services.Orchestrations.SqlMaskFunctions.SqlMaskFunctionOrchestrationService functions = new sql_storage_engine.Services.Orchestrations.SqlMaskFunctions.SqlMaskFunctionOrchestrationService();
    private readonly sql_storage_engine.Services.Orchestrations.SqlMaskValues.SqlMaskValueOrchestrationService values = new sql_storage_engine.Services.Orchestrations.SqlMaskValues.SqlMaskValueOrchestrationService();
    public SqlMaskContext ReadSqlMaskContext(SqlMaskContext sqlMaskContext) =>
        TryCatch(operation: () =>
    {
        ValidateReadSqlMaskContext(sqlMaskContext: sqlMaskContext);
        return functions.ReadSqlMaskContext(sqlMaskContext: sqlMaskContext);
    });

    public SqlMaskContext CheckSqlMaskContext(SqlMaskContext sqlMaskContext) =>
        TryCatch(operation: () =>
    {
        ValidateCheckSqlMaskContext(sqlMaskContext: sqlMaskContext);
        return functions.CheckSqlMaskContext(sqlMaskContext: sqlMaskContext);
    });

    public SqlMaskContext ApplySqlMaskContext(SqlMaskContext sqlMaskContext) =>
        TryCatch(operation: () =>
    {
        ValidateApplySqlMaskContext(sqlMaskContext: sqlMaskContext);
        return values.ApplySqlMaskContext(sqlMaskContext: sqlMaskContext);
    });
}