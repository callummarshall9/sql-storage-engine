// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Spatial;

namespace sql_storage_engine.Services.Processings.SpatialWkts;
internal interface ISqlSpatialShapeWktProcessingService
{
    SqlSpatialShape ParseSqlSpatialShape(string source);
}