// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Managements.SqlValues;
internal interface ISqlValueRequestManagementService
{
    SqlValueRequest CreateSqlValueRequest(SqlValueRequest sqlValueRequest);

    SqlValueRequest CompareSqlValueRequest(SqlValueRequest sqlValueRequest);

    SqlValueRequest ReadBinarySqlValueRequest(SqlValueRequest sqlValueRequest);

    SqlValueRequest ReadVectorSqlValueRequest(SqlValueRequest sqlValueRequest);

    SqlValueRequest MeasureVectorSqlValueRequest(SqlValueRequest sqlValueRequest);

    SqlValueRequest ConvertSqlValueRequest(SqlValueRequest sqlValueRequest);

    SqlValueRequest ValidateSqlValueRequestRow(SqlValueRequest sqlValueRequest);

    SqlValueRequest PrepareSqlValueRequestDefinition(SqlValueRequest sqlValueRequest);

    SqlValueRequest ValidateSqlValueRequestValue(SqlValueRequest sqlValueRequest);

    SqlValueRequest MeasureSpatialSqlValueRequest(SqlValueRequest sqlValueRequest);
}