// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Orchestrations.SqlValueComparisons;
internal interface ISqlTextVariantOrchestrationService
{
    SqlValueComparisonContext PrepareSqlValueComparisonContext(SqlValueComparisonContext sqlValueComparisonContext);

    SqlValueComparisonContext CompareSqlValueComparisonContext(SqlValueComparisonContext sqlValueComparisonContext);
}