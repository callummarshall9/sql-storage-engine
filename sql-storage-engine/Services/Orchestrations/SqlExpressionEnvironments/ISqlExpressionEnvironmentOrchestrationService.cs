// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Orchestrations.SqlExpressionEnvironments;
internal interface ISqlExpressionEnvironmentOrchestrationService
{
    SqlExpressionContext ReadSqlExpressionContextTimestamp(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext CreateSqlExpressionContextIdentifier(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext RegisterSqlExpressionContext(SqlExpressionContext sqlExpressionContext);

    SqlExpressionContext EvaluateSqlExpressionContext(SqlExpressionContext sqlExpressionContext);
}