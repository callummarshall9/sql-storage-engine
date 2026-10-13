// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Services.Coordinations.SqlSpatials;
internal interface ISqlSpatialCoordinationService
{
    SqlSpatial CreateSqlSpatial(SqlSpatial sqlSpatial);

    string FormatSqlSpatial(SqlSpatial sqlSpatial);

    string FormatSqlSpatialGml(SqlSpatial sqlSpatial);

    string ReadSqlSpatialGeometryType(SqlSpatial sqlSpatial);

    int ReadSqlSpatialDimension(SqlSpatial sqlSpatial);

    bool CheckEmptySqlSpatial(SqlSpatial sqlSpatial);

    bool CheckValidSqlSpatial(SqlSpatial sqlSpatial);

    int CountSqlSpatialPoints(SqlSpatial sqlSpatial);

    double MeasureSqlSpatialLength(SqlSpatial sqlSpatial);

    double MeasureSqlSpatialArea(SqlSpatial sqlSpatial);

    (double MinX, double MinY, double MaxX, double MaxY) ReadSqlSpatialBounds(SqlSpatial sqlSpatial);

    SqlSpatial CreateSqlSpatialEnvelope(SqlSpatial sqlSpatial);

    SqlSpatial? ReadSqlSpatialPoint(SqlSpatial sqlSpatial, int position);

    SqlSpatial? ReadSqlSpatialStartPoint(SqlSpatial sqlSpatial);

    SqlSpatial? ReadSqlSpatialEndPoint(SqlSpatial sqlSpatial);

    bool? CheckEquivalentSqlSpatials(SqlSpatial[] sqlSpatials);

    bool? CheckIntersectingSqlSpatials(SqlSpatial[] sqlSpatials);

    bool? CheckContainingSqlSpatials(SqlSpatial[] sqlSpatials);

    bool? CheckWithinSqlSpatials(SqlSpatial[] sqlSpatials);

    bool? CheckDisjointSqlSpatials(SqlSpatial[] sqlSpatials);

    double? MeasureSqlSpatialDistance(SqlSpatial[] sqlSpatials);

    byte[] EncodeSqlSpatial(SqlSpatial sqlSpatial);

    byte[] EncodeSqlSpatialWkb(SqlSpatial sqlSpatial);

    SqlSpatial DecodeSqlSpatial(byte[] value, SqlSpatialKind expectedKind);

    SqlSpatial DecodeSqlSpatialWkb(SqlSpatialKind kind, byte[] value, int srid);
}