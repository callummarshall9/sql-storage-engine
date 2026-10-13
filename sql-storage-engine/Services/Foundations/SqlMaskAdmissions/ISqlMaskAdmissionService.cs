// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.SqlMaskAdmissions;
internal interface ISqlMaskAdmissionService
{
    SqlMaskContext CheckSqlMaskContext(SqlMaskContext sqlMaskContext);
}