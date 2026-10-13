// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Spatial;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.SpatialWkbs;
internal sealed partial class SqlSpatialShapeWkbProcessingService
{
    private static void ValidateSqlSpatialShapeWkb(SqlSpatialShape sqlSpatialShape)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlSpatialShape);
        ArgumentNullException.ThrowIfNull(argument: sqlSpatialShape.Points);
        ArgumentNullException.ThrowIfNull(argument: sqlSpatialShape.Children);
    }

    private static void ValidateSqlSpatialShapeWkbSource(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            throw new StorageFormatException(message: "WKB is truncated.");
        }
    }

    private static void ValidateDepth(int depth)
    {
        if (depth > 128)
        {
            throw new StorageFormatException(message: "WKB exceeds the supported nesting depth.");
        }
    }
}