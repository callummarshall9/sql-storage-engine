using AwesomeAssertions;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;

using sql_storage_engine.Models.Rows;
namespace sql_storage_engine.UnitTests;

public sealed class SpatialArchitectureTests
{
    [TestCase(SqlSpatialKind.Geometry, 0, "020100000000010001000000000000000000F03F00000000000000400000000000")]
    [TestCase(SqlSpatialKind.Geography, 4326, "0202E6100000010001000000000000000000F03F00000000000000400000000000")]
    public void NativePointBytesAndCoordinatesRemainStable(SqlSpatialKind kind, int srid, string hex)
    {
        var value = sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: kind,srid: srid,wellKnownText: "POINT (1 2)");
        var bytes = sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.EncodeSqlSpatial(sqlSpatial: value);
        bytes.Should().Equal(Convert.FromHexString(hex));
        var decoded = sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.DecodeSqlSpatial(value: bytes,expectedKind: kind);
        decoded.WellKnownText.Should().Be("POINT (1 2)");
        decoded.Srid.Should().Be(srid);
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.ReadSqlSpatialDimension(sqlSpatial: decoded).Should().Be(0);
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CountSqlSpatialPoints(sqlSpatial: decoded).Should().Be(1);
        for (var length = 0; length < bytes.Length; length++)
            ((Func<SqlSpatial>)(() => sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.DecodeSqlSpatial(bytes.AsSpan(0, length), kind))).Should().Throw<StorageFormatException>();
    }

    [Test]
    public void NestedShapeFormattingDimensionAndCoordinateOrderRemainStable()
    {
        var value = sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geometry,srid: 0,wellKnownText: "GEOMETRYCOLLECTION (POINT (1 2),LINESTRING (3 4,5 6))");
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.ReadSqlSpatialDimension(sqlSpatial: value).Should().Be(1);
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CountSqlSpatialPoints(sqlSpatial: value).Should().Be(3);
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.ReadSqlSpatialPoint(sqlSpatial: value,position: 1)!.WellKnownText.Should().Be("POINT (1 2)");
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.ReadSqlSpatialPoint(sqlSpatial: value,position: 3)!.WellKnownText.Should().Be("POINT (5 6)");
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.DecodeSqlSpatial(value: sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.EncodeSqlSpatial(sqlSpatial: value),expectedKind: value.Kind).WellKnownText.Should().Be(value.WellKnownText);
        Parallel.For(0, 64, _ => sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.DecodeSqlSpatial(value: sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.EncodeSqlSpatial(sqlSpatial: value),expectedKind: value.Kind).WellKnownText.Should().Be(value.WellKnownText));
    }

    [Test]
    public void NativeSpatialDepthIsBoundedBeforeUnboundedRecursion()
    {
        var bytes = new List<byte> { 2, 1, 0, 0, 0, 0 };
        for (var depth = 0; depth < 130; depth++)
            bytes.AddRange(new byte[] { 7, 0, 0, 0, 0, 0, 1, 0, 0, 0 });
        bytes.AddRange(sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.EncodeSqlSpatial(sqlSpatial: sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geometry,srid: 0,wellKnownText: "POINT (1 2)")).AsSpan(6).ToArray());
        ((Func<SqlSpatial>)(() => sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.DecodeSqlSpatial(bytes.ToArray(), SqlSpatialKind.Geometry))).Should().Throw<StorageFormatException>();
    }
}