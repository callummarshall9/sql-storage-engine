using AwesomeAssertions;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;

using sql_storage_engine.Models.Rows;
namespace sql_storage_engine.UnitTests;

public sealed class SpatialTextWkbArchitectureTests
{
    [TestCase("0101000000000000000000F03F0000000000000040")]
    [TestCase("00000000013FF00000000000004000000000000000")]
    public void WkbAcceptsBothByteOrdersAndWritesCanonicalLittleEndian(string hex)
    {
        var input = Convert.FromHexString(hex);
        var value = sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.DecodeSqlSpatialWkb(SqlSpatialKind.Geometry, input);
        value.WellKnownText.Should().Be("POINT (1 2)");
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.EncodeSqlSpatialWkb(sqlSpatial: value).Should().Equal(Convert.FromHexString("0101000000000000000000F03F0000000000000040"));
        for(var length = 0; length < input.Length; length++)
            ((Func<SqlSpatial>)(() => sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.DecodeSqlSpatialWkb(SqlSpatialKind.Geometry, input.AsSpan(0, length)))).Should().Throw<StorageFormatException>();
    }

    [Test]
    public void MixedEndianWkbCollectionsAndWrongChildTypesAreHandled()
    {
        var bytes = Convert.FromHexString("01070000000100000000000000013FF00000000000004000000000000000");
        var value = sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.DecodeSqlSpatialWkb(SqlSpatialKind.Geometry, bytes);
        value.WellKnownText.Should().Be("GEOMETRYCOLLECTION (POINT (1 2))");
        ((Func<SqlSpatial>)(() => sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.DecodeSqlSpatialWkb(SqlSpatialKind.Geometry, Convert.FromHexString("010400000001000000010200000000000000")))).Should().Throw<StorageFormatException>();
        var deep = new List<byte>();
        for(var depth = 0; depth < 130; depth++) deep.AddRange(Convert.FromHexString("010700000001000000"));
        deep.AddRange(sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.EncodeSqlSpatialWkb(sqlSpatial: sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geometry,srid: 0,wellKnownText: "POINT (1 2)")));
        ((Func<SqlSpatial>)(() => sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.DecodeSqlSpatialWkb(SqlSpatialKind.Geometry, deep.ToArray()))).Should().Throw<StorageFormatException>();
    }

    [TestCase("POINT Z (1 2 3)", 1)]
    [TestCase("POINT M (1 2 9)", 2)]
    [TestCase("POINT ZM (1 2 3 9)", 3)]
    public void WktDimensionMarkersPreserveNativeCoordinateFlags(string text, byte flags)
    {
        var value = sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geometry,srid: 0,wellKnownText: text);
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.EncodeSqlSpatial(sqlSpatial: value)[28].Should().Be(flags);
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.DecodeSqlSpatial(value: sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.EncodeSqlSpatial(sqlSpatial: value),expectedKind: value.Kind).WellKnownText.Should().Be(value.WellKnownText);
    }

    [Test]
    public void MalformedAndOverNestedWktKeepFormatFailureSemantics()
    {
        foreach(var text in new[] { "POINT (1)", "POINT (1 2) trailing", "POINT (NaN 2)", "COMPOUNDCURVE (" })
            ((Func<SqlSpatial>)(() => sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geometry,srid: 0,wellKnownText: text))).Should().Throw<FormatException>();
        var deep = string.Concat(Enumerable.Repeat("GEOMETRYCOLLECTION (", 130)) + "POINT (1 2)" + new string(')', 130);
        ((Func<SqlSpatial>)(() => sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(SqlSpatialKind.Geometry, 0, deep))).Should().Throw<FormatException>();
    }
}