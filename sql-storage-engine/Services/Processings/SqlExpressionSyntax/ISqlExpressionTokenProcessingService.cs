// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.SqlExpressionSyntax;
internal interface ISqlExpressionTokenProcessingService
{
    SqlExpressionContext TokenizeSqlExpressionContext(SqlExpressionContext sqlExpressionContext);
}