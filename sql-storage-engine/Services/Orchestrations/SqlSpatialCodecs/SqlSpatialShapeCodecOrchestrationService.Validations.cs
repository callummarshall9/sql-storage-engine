// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Spatial;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Services.Orchestrations.SqlSpatialCodecs;
internal sealed partial class SqlSpatialShapeCodecOrchestrationService
{
    private static void ValidateShape(SqlSpatialKind kind, int srid, SqlSpatialShape sqlSpatialShape)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlSpatialShape);

        if (!Enum.IsDefined(value: kind))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(kind));
        }
    }

    private static void ValidateShape(SqlSpatialShape sqlSpatialShape) =>
        ArgumentNullException.ThrowIfNull(argument: sqlSpatialShape);

    private static void ValidateBytes(byte[] value) =>
        ArgumentNullException.ThrowIfNull(argument: value);
}