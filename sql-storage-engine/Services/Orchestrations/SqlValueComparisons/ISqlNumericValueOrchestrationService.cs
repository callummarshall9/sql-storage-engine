// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Orchestrations.SqlValueComparisons;
internal interface ISqlNumericValueOrchestrationService
{
    SqlValueComparisonContext CompareSqlValueComparisonContext(SqlValueComparisonContext sqlValueComparisonContext);

    SqlValueComparisonContext ParseSqlValueComparisonContext(SqlValueComparisonContext sqlValueComparisonContext);

    SqlValueComparisonContext PrepareSqlValueComparisonContext(SqlValueComparisonContext sqlValueComparisonContext);

    SqlValueComparisonContext CalculateSqlValueComparisonContext(SqlValueComparisonContext sqlValueComparisonContext);
}