// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Orchestrations.SqlExpressionSyntax;
internal interface ISqlExpressionSyntaxOrchestrationService
{
    SqlExpressionContext TokenizeSqlExpressionContext(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext AdvanceSqlExpressionContext(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext TakeSqlExpressionContext(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext RequireSqlExpressionContext(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext CompleteSqlExpressionContext(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext ReadSqlExpressionContextComputedOrder(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext ReadSqlExpressionContextColumnReference(SqlExpressionContext sqlExpressionContext);
}