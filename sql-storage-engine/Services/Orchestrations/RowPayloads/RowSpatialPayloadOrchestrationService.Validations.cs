// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Spatial;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Services.Orchestrations.RowPayloads;
internal sealed partial class RowSpatialPayloadOrchestrationService
{
    private static void ValidateContext(RowPayloadContext rowPayloadContext)
    {
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext);
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext.Column);
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext.Column.Type);
    }

    private static void ValidateValue(RowPayloadContext rowPayloadContext)
    {
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext);
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext.Value);
        Validate(inputs: [(rowPayloadContext.Value is not SpatialSqlValue, nameof(rowPayloadContext.Value))]);
        ArgumentNullException.ThrowIfNull(argument: ((SpatialSqlValue)rowPayloadContext.Value).Value);
    }

    private static void ValidateSource(RowPayloadContext rowPayloadContext)
    {
        ValidateContext(rowPayloadContext: rowPayloadContext);
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext.Source);
    }

    private static void Validate(ReadOnlySpan<(bool Invalid, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentOutOfRangeException(paramName: input.ParameterName);
            }
        }
    }

    private static void ValidateSpatialKind(SqlSpatialShape shape, SqlSpatialKind kind)
    {
        if (shape.Type == SqlSpatialShapeType.FullGlobe && kind != SqlSpatialKind.Geography)
        {
            throw new FormatException(message: "FULLGLOBE is valid only for geography.");
        }

        if (kind == SqlSpatialKind.Geography)
        {
            ValidateGeography(shape: shape);
        }
    }

    private static void ValidateGeography(SqlSpatialShape shape)
    {
        foreach (var point in shape.Points)
        {
            if (point.X is < -180 or > 180 || point.Y is < -90 or > 90)
            {
                throw new FormatException(message: "Geography coordinates are outside longitude/latitude bounds.");
            }
        }

        foreach (var child in shape.Children)
        {
            ValidateGeography(shape: child);
        }
    }
}