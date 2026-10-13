// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Coordinations.SqlTypes;
internal sealed partial class SqlTypeContextCoordinationService
{
    public SqlTypeContext MeasureSqlTypeContextSpatialDistance(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContextSpatialDistance(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            DistanceResult = spatial.MeasureSqlSpatialDistance(sqlSpatials: sqlTypeContext.SpatialSources!)
        };
    });

    private static void ValidateSqlTypeContextSpatialDistance(SqlTypeContext sqlTypeContext)
    {
        ValidateSpatialDistanceInputs(inputs: [(sqlTypeContext is null, nameof(sqlTypeContext)), (sqlTypeContext?.SpatialSources is null || sqlTypeContext.SpatialSources.Length != 2, nameof(SqlTypeContext.SpatialSources))]);
    }

    private static void ValidateSpatialDistanceInputs((bool Invalid, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: "Two spatial values are required.", paramName: input.ParameterName);
            }
        }
    }
}