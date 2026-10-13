using System.Xml.Linq;
using AwesomeAssertions;
using sql_storage_engine.Exposures.Spatial;
using sql_storage_engine.Models.Spatial;
using sql_storage_engine.Rows;

using sql_storage_engine.Models.Rows;
namespace sql_storage_engine.UnitTests;

public sealed class SpatialGmlArchitectureTests
{
    [TestCase("POINT (1 2)")]
    [TestCase("LINESTRING (1 2,3 4)")]
    [TestCase("LINESTRING EMPTY")]
    [TestCase("POLYGON ((0 0,10 0,10 10,0 0),(1 1,2 1,2 2,1 1))")]
    [TestCase("GEOMETRYCOLLECTION (POINT (1 2),LINESTRING EMPTY,POLYGON EMPTY)")]
    [TestCase("MULTIPOINT EMPTY")]
    public void GmlMatchesFrameworkXmlSerialization(string text)
    {
        var shape = SpatialWktExposure.ParseSqlSpatialShape(text);
        var expected = Element(shape,4326).ToString(SaveOptions.DisableFormatting);
        var spatial = sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geometry,srid: 4326,wellKnownText: text);
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.FormatSqlSpatialGml(sqlSpatial: spatial).Should().Be(expected);
        XDocument.Parse(sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.FormatSqlSpatialGml(sqlSpatial: spatial)).Root!.Name.NamespaceName.Should().Be("http://www.opengis.net/gml");
    }

    private static XElement Element(SqlSpatialShape shape, int srid)
    {
        XNamespace gml = "http://www.opengis.net/gml";
        var attributes = new XAttribute("srsName", $"urn:ogc:def:crs:EPSG::{srid}");
        return shape.Type switch
        {
            SqlSpatialShapeType.Point => new XElement(gml + "Point", attributes, new XElement(gml + "pos", SpatialShapeExposure.FormatSqlSpatialCoordinate(shape.Points.Single()))),
            SqlSpatialShapeType.LineString => new XElement(gml + "LineString", attributes, new XElement(gml + "posList", string.Join(' ', shape.Points.Select(SpatialShapeExposure.FormatSqlSpatialCoordinate)))),
            SqlSpatialShapeType.Polygon => new XElement(gml + "Polygon", attributes, shape.Children.Select((ring, index) => new XElement(gml + (index == 0 ? "exterior" : "interior"), new XElement(gml + "LinearRing", new XElement(gml + "posList", string.Join(' ', ring.Points.Select(SpatialShapeExposure.FormatSqlSpatialCoordinate))))))),
            _ => new XElement(gml + "MultiGeometry", attributes, shape.Children.Select(child => new XElement(gml + "geometryMember", Element(child, srid))))
        };
    }
}