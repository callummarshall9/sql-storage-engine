// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Orchestrations.SqlMaskFunctions;
internal interface ISqlMaskFunctionOrchestrationService
{
    SqlMaskContext ReadSqlMaskContext(SqlMaskContext sqlMaskContext);

    SqlMaskContext CheckSqlMaskContext(SqlMaskContext sqlMaskContext);
}