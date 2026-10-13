// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Coordinations.SqlValueComparisons;
internal interface ISqlValueComparisonCoordinationService
{
    SqlValueComparisonContext CompareSqlValueComparisonContext(SqlValueComparisonContext sqlValueComparisonContext);

    SqlValueComparisonContext ParseSqlValueComparisonContext(SqlValueComparisonContext sqlValueComparisonContext);

    SqlValueComparisonContext PrepareSqlValueComparisonContext(SqlValueComparisonContext sqlValueComparisonContext);

    SqlValueComparisonContext CalculateSqlValueComparisonContext(SqlValueComparisonContext sqlValueComparisonContext);
}