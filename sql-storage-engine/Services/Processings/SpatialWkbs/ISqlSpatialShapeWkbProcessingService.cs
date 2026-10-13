// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Spatial;

namespace sql_storage_engine.Services.Processings.SpatialWkbs;
internal interface ISqlSpatialShapeWkbProcessingService
{
    byte[] EncodeSqlSpatialShapeWkb(SqlSpatialShape sqlSpatialShape);

    SqlSpatialShape DecodeSqlSpatialShapeWkb(ReadOnlySpan<byte> value);
}