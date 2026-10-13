// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.SqlExpressionSyntax;
internal interface ISqlExpressionCursorProcessingService
{
    SqlExpressionContext AdvanceSqlExpressionContext(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext TakeSqlExpressionContext(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext RequireSqlExpressionContext(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext CompleteSqlExpressionContext(SqlExpressionContext sqlExpressionContext);
}