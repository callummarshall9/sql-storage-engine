// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.SqlMaskValues;
internal interface ISqlMaskValueService
{
    SqlMaskContext PrepareSqlMaskContextDateTime(SqlMaskContext sqlMaskContext);

    SqlMaskContext ApplySqlMaskContext(SqlMaskContext sqlMaskContext);
}