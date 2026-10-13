// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Spatial;
using sql_storage_engine.Services.Processings.SpatialWkts;

namespace sql_storage_engine.Exposures.Spatial;
internal static class SpatialWktExposure
{
    private static readonly ISqlSpatialShapeWktProcessingService service = new SqlSpatialShapeWktProcessingService();
    public static SqlSpatialShape ParseSqlSpatialShape(string source) =>
        service.ParseSqlSpatialShape(source: source);
}