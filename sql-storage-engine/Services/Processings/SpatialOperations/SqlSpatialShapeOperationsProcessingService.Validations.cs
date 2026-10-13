// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Spatial;

namespace sql_storage_engine.Services.Processings.SpatialOperations;
internal sealed partial class SqlSpatialShapeOperationsProcessingService
{
    private static void ValidateSqlSpatialShape(SqlSpatialShape sqlSpatialShape)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlSpatialShape);
        ArgumentNullException.ThrowIfNull(argument: sqlSpatialShape.Points);
        ArgumentNullException.ThrowIfNull(argument: sqlSpatialShape.Children);
    }

    private static void ValidateSqlSpatialShapeMeasurement(SqlSpatialShape sqlSpatialShape, SqlSpatialKind kind)
    {
        ValidateSqlSpatialShape(sqlSpatialShape: sqlSpatialShape);

        if (!Enum.IsDefined(value: kind))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(kind));
        }
    }

    private static void ValidateSqlSpatialShapes(SqlSpatialShape[] sqlSpatialShapes)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlSpatialShapes);

        if (sqlSpatialShapes.Length != 2)
        {
            throw new ArgumentException(message: "Spatial comparison requires two shapes.");
        }

        foreach (var shape in sqlSpatialShapes)
        {
            ValidateSqlSpatialShape(sqlSpatialShape: shape);
        }
    }

    private static void ValidateSqlSpatialShapePairMeasurement(SqlSpatialShape[] sqlSpatialShapes, SqlSpatialKind kind)
    {
        ValidateSqlSpatialShapes(sqlSpatialShapes: sqlSpatialShapes);

        if (!Enum.IsDefined(value: kind))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(kind));
        }
    }
}