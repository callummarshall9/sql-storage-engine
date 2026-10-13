// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Coordinations.SqlExpressions;
internal interface ISqlExpressionRuntimeCoordinationService
{
    SqlExpressionContext TokenizeSqlExpressionContext(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext AdvanceSqlExpressionContext(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext TakeSqlExpressionContext(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext RequireSqlExpressionContext(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext CompleteSqlExpressionContext(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext ReadSqlExpressionContextTimestamp(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext CreateSqlExpressionContextIdentifier(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext RegisterSqlExpressionContext(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext EvaluateSqlExpressionContext(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext ReadSqlExpressionContextComputedOrder(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext ReadSqlExpressionContextColumnReference(SqlExpressionContext sqlExpressionContext);
}