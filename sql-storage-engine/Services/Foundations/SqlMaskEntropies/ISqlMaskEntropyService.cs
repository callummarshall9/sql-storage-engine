// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.SqlMaskEntropies;
internal interface ISqlMaskEntropyService
{
    SqlMaskContext ReadSqlMaskContext(SqlMaskContext sqlMaskContext);
}