// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Managements.SqlValues;
internal sealed partial class SqlValueRequestManagementService
{
    public SqlValueRequest ValidateSqlValueRequestValue(SqlValueRequest sqlValueRequest) =>
        TryCatch(operation: () =>
    {
        ValidateSingleValueRequest(sqlValueRequest: sqlValueRequest);
        types.ProcessSqlTypeContextValidate76(sqlTypeContext: new SqlTypeContext { SqlTypeSource = (SqlType)sqlValueRequest.Input!, InputSqlValueValue = (SqlValue)sqlValueRequest.Other!, InputstringColumnName = sqlValueRequest.Third as string });
        return sqlValueRequest;
    });

    public SqlValueRequest MeasureSpatialSqlValueRequest(SqlValueRequest sqlValueRequest) =>
        TryCatch(operation: () =>
    {
        ValidateSpatialValueRequest(sqlValueRequest: sqlValueRequest);
        var values = sqlValueRequest.SqlValues!;

        var distance = types.MeasureSqlTypeContextSpatialDistance(sqlTypeContext: new SqlTypeContext { SpatialSources = [((SpatialSqlValue)values[0]).Value, ((SpatialSqlValue)values[1]).Value] })
            .DistanceResult;

        return sqlValueRequest with
        {
            DistanceResult = distance ?? double.NaN
        };
    });

    private static void ValidateSingleValueRequest(SqlValueRequest sqlValueRequest) =>
        ValidatePublicQueryInputs(inputs: [(sqlValueRequest is null, nameof(sqlValueRequest)), (sqlValueRequest?.Input is not SqlType, nameof(SqlValueRequest.Input)), (sqlValueRequest?.Other is not SqlValue, nameof(SqlValueRequest.Other))]);

    private static void ValidateSpatialValueRequest(SqlValueRequest sqlValueRequest) =>
        ValidatePublicQueryInputs(inputs: [(sqlValueRequest is null, nameof(sqlValueRequest)), (sqlValueRequest?.SqlValues is null || sqlValueRequest.SqlValues.Length != 2 || sqlValueRequest.SqlValues[0] is not SpatialSqlValue || sqlValueRequest.SqlValues[1] is not SpatialSqlValue, nameof(SqlValueRequest.SqlValues))]);

    private static void ValidatePublicQueryInputs((bool Invalid, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: "A typed query value is required.", paramName: input.ParameterName);
            }
        }
    }
}