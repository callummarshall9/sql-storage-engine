// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Orchestrations.SqlMaskFunctions;
internal sealed partial class SqlMaskFunctionOrchestrationService : ISqlMaskFunctionOrchestrationService
{
    private readonly sql_storage_engine.Services.Foundations.SqlMaskFunctions.SqlMaskFunctionService functions = new sql_storage_engine.Services.Foundations.SqlMaskFunctions.SqlMaskFunctionService();
    private readonly sql_storage_engine.Services.Foundations.SqlMaskAdmissions.SqlMaskAdmissionService admission = new sql_storage_engine.Services.Foundations.SqlMaskAdmissions.SqlMaskAdmissionService();
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
        return admission.CheckSqlMaskContext(sqlMaskContext: functions.ReadSqlMaskContext(sqlMaskContext: sqlMaskContext));
    });
}