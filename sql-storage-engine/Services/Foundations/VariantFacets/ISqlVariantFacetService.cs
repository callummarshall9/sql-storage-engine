// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.VariantFacets;
internal interface ISqlVariantFacetService
{
    SqlValueComparisonContext PrepareSqlValueComparisonContext(SqlValueComparisonContext sqlValueComparisonContext);
}