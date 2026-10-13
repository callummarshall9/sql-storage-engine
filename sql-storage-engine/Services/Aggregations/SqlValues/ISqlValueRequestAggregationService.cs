// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Aggregations.SqlValues;
internal interface ISqlValueRequestAggregationService
{
    SqlValueRequest CreateSqlValueRequest(SqlValueRequest sqlValueRequest);

    SqlValueRequest CompareSqlValueRequest(SqlValueRequest sqlValueRequest);

    SqlValueRequest ReadBinarySqlValueRequest(SqlValueRequest sqlValueRequest);

    SqlValueRequest ReadVectorSqlValueRequest(SqlValueRequest sqlValueRequest);

    SqlValueRequest MeasureVectorSqlValueRequest(SqlValueRequest sqlValueRequest);
}