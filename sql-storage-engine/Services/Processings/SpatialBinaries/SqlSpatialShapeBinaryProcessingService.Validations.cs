// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Spatial;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.SpatialBinaries;
internal sealed partial class SqlSpatialShapeBinaryProcessingService
{
    private static void ValidateSqlSpatialShapeSource(SqlSpatialKind kind, int srid, SqlSpatialShape sqlSpatialShape)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlSpatialShape);
        ArgumentNullException.ThrowIfNull(argument: sqlSpatialShape.Points);
        ArgumentNullException.ThrowIfNull(argument: sqlSpatialShape.Children);
    }

    private static void ValidateSqlSpatialShapeBytes(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            throw new StorageFormatException(message: "Spatial representation is truncated.");
        }
    }
}