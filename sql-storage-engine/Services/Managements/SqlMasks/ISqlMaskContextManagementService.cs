// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Managements.SqlMasks;
internal interface ISqlMaskContextManagementService
{
    SqlMaskContext CheckSqlMaskContext(SqlMaskContext sqlMaskContext);

    SqlMaskContext ApplySqlMaskContext(SqlMaskContext sqlMaskContext);
}