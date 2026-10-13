using AwesomeAssertions;
using sql_storage_engine.Rows;

using sql_storage_engine.Models.Rows;
namespace sql_storage_engine.UnitTests;

public sealed class SpatialOperationsArchitectureTests
{
    [Test]
    public void PolygonHoleAreaAndContainmentRemainConsistent()
    {
        var polygon = sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geometry,srid: 0,wellKnownText: "POLYGON ((0 0,10 0,10 10,0 10,0 0),(3 3,7 3,7 7,3 7,3 3))");
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CheckValidSqlSpatial(sqlSpatial: polygon).Should().BeTrue();
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.MeasureSqlSpatialArea(sqlSpatial: polygon).Should().Be(84);
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CheckContainingSqlSpatials(sqlSpatials: [polygon, sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geometry,srid: 0,wellKnownText: "POINT (1 1)")]).Should().BeTrue();
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CheckContainingSqlSpatials(sqlSpatials: [polygon, sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geometry,srid: 0,wellKnownText: "POINT (5 5)")]).Should().BeFalse();
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatialEnvelope(sqlSpatial: polygon).WellKnownText.Should().Be("POLYGON ((0 0,10 0,10 10,0 10,0 0))");
    }

    [Test]
    public void DistanceIntersectionsAndSridCompatibilityRemainStable()
    {
        var first = sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geometry,srid: 0,wellKnownText: "POINT (0 0)");
        var second = sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geometry,srid: 0,wellKnownText: "POINT (3 4)");
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.MeasureSqlSpatialDistance(sqlSpatials: [first, second]).Should().Be(5);
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.MeasureSqlSpatialDistance(sqlSpatials: [second, first]).Should().Be(5);
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CheckIntersectingSqlSpatials(sqlSpatials: [first, second]).Should().BeFalse();
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.MeasureSqlSpatialDistance(sqlSpatials: [first, sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geometry,srid: 1,wellKnownText: "POINT (3 4)")]).Should().BeNull();
        var line = sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geometry,srid: 0,wellKnownText: "LINESTRING (0 0,3 4)");
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.MeasureSqlSpatialLength(sqlSpatial: line).Should().Be(5);
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CheckIntersectingSqlSpatials(sqlSpatials: [line, first]).Should().BeTrue();
        Parallel.For(0, 64, _ => sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CheckIntersectingSqlSpatials(sqlSpatials: [line, second]).Should().BeTrue());
    }

    [Test]
    public void GeographyRejectsInvalidLongitudeAndLatitudeWithRangeMetadata()
    {
        ((Func<SqlSpatial>)(() => sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geography,srid: 4326,wellKnownText: "POINT (181 0)"))).Should().Throw<ArgumentOutOfRangeException>().WithParameterName("shape");
        ((Func<SqlSpatial>)(() => sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geography,srid: 4326,wellKnownText: "POINT (0 91)"))).Should().Throw<ArgumentOutOfRangeException>().WithParameterName("shape");
    }
}