// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Orchestrations.SqlMaskValues;
internal interface ISqlMaskValueOrchestrationService
{
    SqlMaskContext ApplySqlMaskContext(SqlMaskContext sqlMaskContext);
}