// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Spatial;

namespace sql_storage_engine.Services.Processings.SpatialWkts;
internal sealed partial class SqlSpatialShapeWktProcessingService : ISqlSpatialShapeWktProcessingService
{
    public SqlSpatialShape ParseSqlSpatialShape(string source) =>
        TryCatch(operation: () =>
    {
        ValidateSqlSpatialShapeWkt(source: source);

        var cursor = new SqlSpatialWktCursor
        {
            Source = source.AsSpan()
        };

        var shape = Shape(cursor: ref cursor);
        White(cursor: ref cursor);

        if (cursor.Position != cursor.Source.Length)
        {
            throw new FormatException(message: "Spatial WKT contains trailing input.");
        }

        return shape;
    });

    private static SqlSpatialShape Shape(ref SqlSpatialWktCursor cursor)
    {
        if (++cursor.Depth > 128)
        {
            throw new FormatException(message: "Spatial WKT exceeds the supported nesting depth.");
        }

        try
        {
            return ReadShapeCore(cursor: ref cursor);
        }
        finally
        {
            cursor.Depth--;
        }
    }

    private static SqlSpatialShape CreateSqlSpatialShape(SqlSpatialShape sqlSpatialShape) =>
        new SqlSpatialShape
    {
        Type = sqlSpatialShape.Type,
        Points = sqlSpatialShape.PointSource?.ToArray() ?? Array.Empty<SqlSpatialCoordinate>(),
        Children = sqlSpatialShape.ChildSource?.ToArray() ?? Array.Empty<SqlSpatialShape>(),
        IsEmpty = sqlSpatialShape.IsEmpty
    };

    private static SqlSpatialShape CreateSqlSpatialShapePoint(SqlSpatialCoordinate point) =>
        new SqlSpatialShape
    {
        Type = SqlSpatialShapeType.Point,
        Points = [point],
        Children = Array.Empty<SqlSpatialShape>()
    };

    private static SqlSpatialShape ReadShapeCore(ref SqlSpatialWktCursor cursor)
    {
        var type = Type(word: Word(cursor: ref cursor));
        White(cursor: ref cursor);
        var dimension = Dimension.None;
        var saved = cursor.Position;

        if (!(cursor.Position == cursor.Source.Length) && char.IsLetter(c: cursor.Source[cursor.Position]))
        {
            dimension = Word(cursor: ref cursor)
            .ToUpperInvariant() switch
            {
                "Z" => Dimension.Z,
                "M" => Dimension.M,
                "ZM" => Dimension.ZM,
                _ => Dimension.None
            };

            if (dimension == Dimension.None)
            {
                cursor.Position = saved;
            }

            White(cursor: ref cursor);
        }

        if (TryWord(cursor: ref cursor, expected: "EMPTY"))
        {
            return CreateSqlSpatialShape(sqlSpatialShape: new sql_storage_engine.Models.Spatial.SqlSpatialShape { PointSource = null, ChildSource = null, IsEmpty = true, Type = type });
        }

        if (type == SqlSpatialShapeType.FullGlobe)
        {
            return CreateSqlSpatialShape(sqlSpatialShape: new sql_storage_engine.Models.Spatial.SqlSpatialShape { PointSource = null, ChildSource = null, IsEmpty = false, Type = type });
        }

        return type switch
        {
            SqlSpatialShapeType.Point => CreateSqlSpatialShape(sqlSpatialShape: new sql_storage_engine.Models.Spatial.SqlSpatialShape { PointSource = [PointBody(cursor: ref cursor, dimension: dimension)], ChildSource = null, IsEmpty = false, Type = type }),
            SqlSpatialShapeType.LineString or SqlSpatialShapeType.CircularString => CreateSqlSpatialShape(sqlSpatialShape: new sql_storage_engine.Models.Spatial.SqlSpatialShape { PointSource = PointListBody(cursor: ref cursor, dimension: dimension), ChildSource = null, IsEmpty = false, Type = type }),
            SqlSpatialShapeType.Polygon => PolygonBody(cursor: ref cursor, type: type, dimension: dimension),
            SqlSpatialShapeType.MultiPoint => MultiPointBody(cursor: ref cursor, dimension: dimension),
            SqlSpatialShapeType.MultiLineString => MultiLineBody(cursor: ref cursor, dimension: dimension),
            SqlSpatialShapeType.MultiPolygon => MultiPolygonBody(cursor: ref cursor, dimension: dimension),
            SqlSpatialShapeType.GeometryCollection => CollectionBody(cursor: ref cursor, type: type),
            SqlSpatialShapeType.CompoundCurve or SqlSpatialShapeType.CurvePolygon => CurveBody(cursor: ref cursor, type: type, dimension: dimension),
            _ => throw new FormatException("Unsupported spatial type.")};
    }

    private static SqlSpatialCoordinate PointBody(ref SqlSpatialWktCursor cursor, Dimension dimension)
    {
        Open(cursor: ref cursor);
        var point = Coordinate(cursor: ref cursor, dimension: dimension);
        Close(cursor: ref cursor);
        return point;
    }

    private static SqlSpatialCoordinate[] PointListBody(ref SqlSpatialWktCursor cursor, Dimension dimension)
    {
        Open(cursor: ref cursor);
        var points = PointList(cursor: ref cursor, dimension: dimension);
        Close(cursor: ref cursor);
        return points;
    }

    private static SqlSpatialShape PolygonBody(ref SqlSpatialWktCursor cursor, SqlSpatialShapeType type, Dimension dimension)
    {
        Open(cursor: ref cursor);
        var rings = new List<SqlSpatialShape>();

        do
        {
            Open(cursor: ref cursor);
            var points = PointList(cursor: ref cursor, dimension: dimension);
            Close(cursor: ref cursor);
            rings.Add(item: CreateSqlSpatialShape(sqlSpatialShape: new sql_storage_engine.Models.Spatial.SqlSpatialShape { PointSource = points, ChildSource = null, IsEmpty = false, Type = SqlSpatialShapeType.LineString }));
        }
        while (Comma(cursor: ref cursor));

        Close(cursor: ref cursor);
        return CreateSqlSpatialShape(sqlSpatialShape: new sql_storage_engine.Models.Spatial.SqlSpatialShape { PointSource = null, ChildSource = rings, IsEmpty = false, Type = type });
    }

    private static SqlSpatialShape MultiPointBody(ref SqlSpatialWktCursor cursor, Dimension dimension)
    {
        Open(cursor: ref cursor);
        var points = new List<SqlSpatialShape>();

        do
        {
            White(cursor: ref cursor);
            var wrapped = !(cursor.Position == cursor.Source.Length) && cursor.Source[cursor.Position] == '(';

            if (wrapped)
            {
                Open(cursor: ref cursor);
            }

            points.Add(item: CreateSqlSpatialShapePoint(point: Coordinate(cursor: ref cursor, dimension: dimension)));

            if (wrapped)
            {
                Close(cursor: ref cursor);
            }
        }
        while (Comma(cursor: ref cursor));

        Close(cursor: ref cursor);
        return CreateSqlSpatialShape(sqlSpatialShape: new sql_storage_engine.Models.Spatial.SqlSpatialShape { PointSource = null, ChildSource = points, IsEmpty = false, Type = SqlSpatialShapeType.MultiPoint });
    }

    private static SqlSpatialShape MultiLineBody(ref SqlSpatialWktCursor cursor, Dimension dimension)
    {
        Open(cursor: ref cursor);
        var lines = new List<SqlSpatialShape>();

        do
        {
            Open(cursor: ref cursor);
            lines.Add(item: CreateSqlSpatialShape(sqlSpatialShape: new sql_storage_engine.Models.Spatial.SqlSpatialShape { PointSource = PointList(cursor: ref cursor, dimension: dimension), ChildSource = null, IsEmpty = false, Type = SqlSpatialShapeType.LineString }));
            Close(cursor: ref cursor);
        }
        while (Comma(cursor: ref cursor));

        Close(cursor: ref cursor);
        return CreateSqlSpatialShape(sqlSpatialShape: new sql_storage_engine.Models.Spatial.SqlSpatialShape { PointSource = null, ChildSource = lines, IsEmpty = false, Type = SqlSpatialShapeType.MultiLineString });
    }

    private static SqlSpatialShape MultiPolygonBody(ref SqlSpatialWktCursor cursor, Dimension dimension)
    {
        Open(cursor: ref cursor);
        var polygons = new List<SqlSpatialShape>();

        do
        {
            polygons.Add(item: PolygonBody(cursor: ref cursor, type: SqlSpatialShapeType.Polygon, dimension: dimension));
        }
        while (Comma(cursor: ref cursor));

        Close(cursor: ref cursor);
        return CreateSqlSpatialShape(sqlSpatialShape: new sql_storage_engine.Models.Spatial.SqlSpatialShape { PointSource = null, ChildSource = polygons, IsEmpty = false, Type = SqlSpatialShapeType.MultiPolygon });
    }

    private static SqlSpatialShape CollectionBody(ref SqlSpatialWktCursor cursor, SqlSpatialShapeType type)
    {
        Open(cursor: ref cursor);
        var children = new List<SqlSpatialShape>();

        do
        {
            children.Add(item: Shape(cursor: ref cursor));
        }
        while (Comma(cursor: ref cursor));

        Close(cursor: ref cursor);
        return CreateSqlSpatialShape(sqlSpatialShape: new sql_storage_engine.Models.Spatial.SqlSpatialShape { PointSource = null, ChildSource = children, IsEmpty = false, Type = type });
    }

    private static SqlSpatialShape CurveBody(ref SqlSpatialWktCursor cursor, SqlSpatialShapeType type, Dimension dimension)
    {
        Open(cursor: ref cursor);
        var children = new List<SqlSpatialShape>();

        do
        {
            White(cursor: ref cursor);

            if (cursor.Position == cursor.Source.Length)
            {
                throw new FormatException(message: "Spatial WKT is truncated.");
            }

            if (cursor.Source[cursor.Position] == '(')
            {
                children.Add(item: CreateSqlSpatialShape(sqlSpatialShape: new sql_storage_engine.Models.Spatial.SqlSpatialShape { PointSource = PointListBody(cursor: ref cursor, dimension: dimension), ChildSource = null, IsEmpty = false, Type = SqlSpatialShapeType.LineString }));
            }
            else
            {
                children.Add(item: Shape(cursor: ref cursor));
            }
        }
        while (Comma(cursor: ref cursor));

        Close(cursor: ref cursor);
        return CreateSqlSpatialShape(sqlSpatialShape: new sql_storage_engine.Models.Spatial.SqlSpatialShape { PointSource = null, ChildSource = children, IsEmpty = false, Type = type });
    }

    private static SqlSpatialCoordinate[] PointList(ref SqlSpatialWktCursor cursor, Dimension dimension)
    {
        var points = new List<SqlSpatialCoordinate>
        {
            Coordinate(cursor: ref cursor, dimension: dimension)
        };

        while (Comma(cursor: ref cursor))
        {
            points.Add(item: Coordinate(cursor: ref cursor, dimension: dimension));
        }

        return points.ToArray();
    }

    private static SqlSpatialCoordinate Coordinate(ref SqlSpatialWktCursor cursor, Dimension dimension)
    {
        var values = new List<double>
        {
            Number(cursor: ref cursor),
            Number(cursor: ref cursor)
        };

        var expected = dimension switch
        {
            Dimension.Z or Dimension.M => 3,
            Dimension.ZM => 4,
            _ => 0
        };

        if (expected == 0)
        {
            while (values.Count < 4 && HasNumber(cursor: ref cursor))
            {
                values.Add(item: Number(cursor: ref cursor));
            }

            expected = values.Count;
        }
        else
        {
            while (values.Count < expected)
            {
                values.Add(item: Number(cursor: ref cursor));
            }
        }

        return dimension switch
        {
            Dimension.M => new sql_storage_engine.Models.Spatial.SqlSpatialCoordinate
            {
                Z = null,
                M = values[2],
                X = values[0],
                Y = values[1]
            },
            Dimension.ZM => new sql_storage_engine.Models.Spatial.SqlSpatialCoordinate
            {
                Z = values[2],
                M = values[3],
                X = values[0],
                Y = values[1]
            },
            _ => new sql_storage_engine.Models.Spatial.SqlSpatialCoordinate
            {
                Z = values.Count > 2 ? values[2] : null,
                M = values.Count > 3 ? values[3] : null,
                X = values[0],
                Y = values[1]
            }
        };
    }

    private static bool HasNumber(ref SqlSpatialWktCursor cursor)
    {
        White(cursor: ref cursor);
        return !(cursor.Position == cursor.Source.Length) && cursor.Source[cursor.Position] is not (',' or ')');
    }

    private static double Number(ref SqlSpatialWktCursor cursor)
    {
        White(cursor: ref cursor);
        var start = cursor.Position;

        while (!(cursor.Position == cursor.Source.Length) && !char.IsWhiteSpace(c: cursor.Source[cursor.Position]) && cursor.Source[cursor.Position] is not (',' or ')'))
        {
            cursor.Position++;
        }

        if (start == cursor.Position || !double.TryParse(s: cursor.Source[start..cursor.Position], style: NumberStyles.Float, provider: CultureInfo.InvariantCulture, result: out var value) || !double.IsFinite(d: value))
        {
            throw new FormatException("Spatial coordinate is invalid.");
        }

        White(cursor: ref cursor);
        return value;
    }

    private static string Word(ref SqlSpatialWktCursor cursor)
    {
        White(cursor: ref cursor);
        var start = cursor.Position;

        while (!(cursor.Position == cursor.Source.Length) && char.IsLetter(c: cursor.Source[cursor.Position]))
        {
            cursor.Position++;
        }

        if (start == cursor.Position)
        {
            throw new FormatException("Expected a spatial type.");
        }

        return cursor.Source[start..cursor.Position].ToString();
    }

    private static bool TryWord(ref SqlSpatialWktCursor cursor, string expected)
    {
        var saved = cursor.Position;

        if ((cursor.Position == cursor.Source.Length) || !char.IsLetter(c: cursor.Source[cursor.Position]))
        {
            return false;
        }

        var word = Word(cursor: ref cursor);

        if (word.Equals(value: expected, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        cursor.Position = saved;
        return false;
    }

    private static void Open(ref SqlSpatialWktCursor cursor)
    {
        White(cursor: ref cursor);
        Expect(cursor: ref cursor, value: '(');
        White(cursor: ref cursor);
    }

    private static void Close(ref SqlSpatialWktCursor cursor)
    {
        White(cursor: ref cursor);
        Expect(cursor: ref cursor, value: ')');
        White(cursor: ref cursor);
    }

    private static bool Comma(ref SqlSpatialWktCursor cursor)
    {
        White(cursor: ref cursor);

        if ((cursor.Position == cursor.Source.Length) || cursor.Source[cursor.Position] != ',')
        {
            return false;
        }

        cursor.Position++;
        White(cursor: ref cursor);
        return true;
    }

    private static void Expect(ref SqlSpatialWktCursor cursor, char value)
    {
        if ((cursor.Position == cursor.Source.Length) || cursor.Source[cursor.Position++] != value)
        {
            throw new FormatException($"Expected '{value}' in spatial WKT.");
        }
    }

    private static void White(ref SqlSpatialWktCursor cursor)
    {
        while (!(cursor.Position == cursor.Source.Length) && char.IsWhiteSpace(c: cursor.Source[cursor.Position]))
        {
            cursor.Position++;
        }
    }

    private static SqlSpatialShapeType Type(string word) =>
        word.ToUpperInvariant() switch
    {
        "POINT" => SqlSpatialShapeType.Point,
        "LINESTRING" => SqlSpatialShapeType.LineString,
        "POLYGON" => SqlSpatialShapeType.Polygon,
        "MULTIPOINT" => SqlSpatialShapeType.MultiPoint,
        "MULTILINESTRING" => SqlSpatialShapeType.MultiLineString,
        "MULTIPOLYGON" => SqlSpatialShapeType.MultiPolygon,
        "GEOMETRYCOLLECTION" => SqlSpatialShapeType.GeometryCollection,
        "CIRCULARSTRING" => SqlSpatialShapeType.CircularString,
        "COMPOUNDCURVE" => SqlSpatialShapeType.CompoundCurve,
        "CURVEPOLYGON" => SqlSpatialShapeType.CurvePolygon,
        "FULLGLOBE" => SqlSpatialShapeType.FullGlobe,
        _ => throw new FormatException($"Unknown spatial type '{word}'.")};
    private enum Dimension : byte
    {
        None,
        Z,
        M,
        ZM
    }
}