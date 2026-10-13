// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Managements.SqlExpressions;
internal interface ISqlExpressionContextManagementService
{
    SqlExpressionContext EvaluateSqlExpressionContext(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext RegisterSqlExpressionContext(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext ReadSqlExpressionContextComputedOrder(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext ReadSqlExpressionContextColumnReference(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext CreateSqlExpressionContextIdentifier(SqlExpressionContext sqlExpressionContext);
}