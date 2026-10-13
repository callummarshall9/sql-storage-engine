// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Coordinations.SqlMasks;
internal interface ISqlMaskContextCoordinationService
{
    SqlMaskContext ReadSqlMaskContext(SqlMaskContext sqlMaskContext);

    SqlMaskContext CheckSqlMaskContext(SqlMaskContext sqlMaskContext);

    SqlMaskContext ApplySqlMaskContext(SqlMaskContext sqlMaskContext);
}