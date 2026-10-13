// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.SqlExpressionEnvironments;
internal interface ISqlExpressionClockService
{
    SqlExpressionContext ReadSqlExpressionContextTimestamp(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext CreateSqlExpressionContextIdentifier(SqlExpressionContext sqlExpressionContext);
}