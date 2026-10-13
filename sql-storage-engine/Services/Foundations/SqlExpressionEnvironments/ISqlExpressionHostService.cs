// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.SqlExpressionEnvironments;
internal interface ISqlExpressionHostService
{
    SqlExpressionContext RegisterSqlExpressionContext(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext EvaluateSqlExpressionContext(SqlExpressionContext sqlExpressionContext);
}