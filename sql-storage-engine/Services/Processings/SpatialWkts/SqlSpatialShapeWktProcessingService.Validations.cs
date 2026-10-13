// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Services.Processings.SpatialWkts;
internal sealed partial class SqlSpatialShapeWktProcessingService
{
    private static void ValidateSqlSpatialShapeWkt(string source) =>
        ArgumentNullException.ThrowIfNull(argument: source);
}