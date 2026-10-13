// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Storage;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Spatial;

namespace sql_storage_engine.Services.Processings.SpatialShapes;
internal sealed partial class SqlSpatialShapeProcessingService : ISqlSpatialShapeProcessingService
{
    public SqlSpatialShape PrepareSqlSpatialShape(SqlSpatialShape sqlSpatialShape) =>
        TryCatch(operation: () =>
    {
        ValidateSqlSpatialShapeInput(sqlSpatialShape: sqlSpatialShape);

        return new SqlSpatialShape
        {
            Type = sqlSpatialShape.Type,
            Points = (sqlSpatialShape.PointSource ?? sqlSpatialShape.Points ?? Array.Empty<SqlSpatialCoordinate>()).ToArray(),
            Children = (sqlSpatialShape.ChildSource ?? sqlSpatialShape.Children ?? Array.Empty<SqlSpatialShape>()).ToArray(),
            IsEmpty = sqlSpatialShape.IsEmpty
        };
    });

    public string FormatSqlSpatialShape(SqlSpatialShape sqlSpatialShape) =>
        TryCatch(operation: () =>
    {
        ValidateSqlSpatialShape(sqlSpatialShape: sqlSpatialShape);
        return FormatShape(shape: sqlSpatialShape);
    });

    public string FormatSqlSpatialShapeCoordinate(SqlSpatialShape sqlSpatialShape) =>
        TryCatch(operation: () =>
    {
        ValidateSqlSpatialShape(sqlSpatialShape: sqlSpatialShape);

        if (sqlSpatialShape.Points.Length != 1)
        {
            throw new ArgumentException(message: "Coordinate formatting requires one point.", paramName: nameof(sqlSpatialShape));
        }

        return FormatCoordinate(point: sqlSpatialShape.Points[0]);
    });

    public int ReadSqlSpatialShapeDimension(SqlSpatialShape sqlSpatialShape) =>
        TryCatch(operation: () =>
    {
        ValidateSqlSpatialShape(sqlSpatialShape: sqlSpatialShape);
        return Dimension(shape: sqlSpatialShape);
    });

    public (double MinX, double MinY, double MaxX, double MaxY) ReadSqlSpatialShapeBounds(SqlSpatialShape sqlSpatialShape) =>
        TryCatch(operation: () =>
    {
        ValidateSqlSpatialShape(sqlSpatialShape: sqlSpatialShape);

        var points = Coordinates(shape: sqlSpatialShape)
            .ToArray();

        if (points.Length == 0)
        {
            return (double.NaN, double.NaN, double.NaN, double.NaN);
        }

        return (points.Min(selector: point => point.X), points.Min(selector: point => point.Y), points.Max(selector: point => point.X), points.Max(selector: point => point.Y));
    });

    public SqlSpatialShape FlattenSqlSpatialShape(SqlSpatialShape sqlSpatialShape) =>
        TryCatch(operation: () =>
    {
        ValidateSqlSpatialShape(sqlSpatialShape: sqlSpatialShape);

        return new SqlSpatialShape
        {
            Type = sqlSpatialShape.Type,
            Points = Coordinates(shape: sqlSpatialShape)
            .ToArray(),
            Children = Array.Empty<SqlSpatialShape>(),
            IsEmpty = sqlSpatialShape.IsEmpty
        };
    });

    private static int Dimension(SqlSpatialShape shape) =>
        shape.Type switch
    {
        SqlSpatialShapeType.Point or SqlSpatialShapeType.MultiPoint => 0,
        SqlSpatialShapeType.LineString or SqlSpatialShapeType.MultiLineString or SqlSpatialShapeType.CircularString or SqlSpatialShapeType.CompoundCurve => 1,
        SqlSpatialShapeType.Polygon or SqlSpatialShapeType.MultiPolygon or SqlSpatialShapeType.CurvePolygon or SqlSpatialShapeType.FullGlobe => 2,
        SqlSpatialShapeType.GeometryCollection => shape.Children.Length == 0 ? -1 : shape.Children.Max(selector: child => Dimension(shape: child)),
        _ => -1
    };

    private static IEnumerable<SqlSpatialCoordinate> Coordinates(SqlSpatialShape shape) =>
        shape.Points.Concat(second: shape.Children.SelectMany(selector: child => Coordinates(shape: child)));

    private static string TypeName(SqlSpatialShapeType type) =>
        type switch
    {
        SqlSpatialShapeType.LineString => "LineString",
        SqlSpatialShapeType.MultiPoint => "MultiPoint",
        SqlSpatialShapeType.MultiLineString => "MultiLineString",
        SqlSpatialShapeType.MultiPolygon => "MultiPolygon",
        SqlSpatialShapeType.GeometryCollection => "GeometryCollection",
        SqlSpatialShapeType.CircularString => "CircularString",
        SqlSpatialShapeType.CompoundCurve => "CompoundCurve",
        SqlSpatialShapeType.CurvePolygon => "CurvePolygon",
        SqlSpatialShapeType.FullGlobe => "FullGlobe",
        _ => type.ToString()};

    private static string FormatCoordinate(SqlSpatialCoordinate point)
    {
        var values = new List<string>
        {
            point.X.ToString(format: "R", provider: CultureInfo.InvariantCulture),
            point.Y.ToString(format: "R", provider: CultureInfo.InvariantCulture)
        };

        if (point.Z is { } z)
        {
            values.Add(item: z.ToString(format: "R", provider: CultureInfo.InvariantCulture));
        }

        if (point.M is { } m)
        {
            values.Add(item: m.ToString(format: "R", provider: CultureInfo.InvariantCulture));
        }

        return string.Join(separator: ' ', values: values);
    }

    private static string FormatShape(SqlSpatialShape shape)
    {
        var name = TypeName(type: shape.Type)
            .ToUpperInvariant();

        if (shape.Type == SqlSpatialShapeType.FullGlobe)
        {
            return name;
        }

        if (shape.IsEmpty)
        {
            return $"{name} EMPTY";
        }

        return shape.Type switch
        {
            SqlSpatialShapeType.Point => $"{name} ({FormatCoordinate(point: shape.Points.Single())})",
            SqlSpatialShapeType.LineString or SqlSpatialShapeType.CircularString => $"{name} ({CoordinatesText(points: shape.Points)})",
            SqlSpatialShapeType.Polygon => $"{name} ({string.Join(separator: ',', values: shape.Children.Select(selector: RingText))})",
            SqlSpatialShapeType.MultiPoint => $"{name} ({string.Join(separator: ',', values: shape.Children.Select(selector: child => $"({FormatCoordinate(point: child.Points.Single())})"))})",
            SqlSpatialShapeType.MultiLineString => $"{name} ({string.Join(separator: ',', values: shape.Children.Select(selector: RingText))})",
            SqlSpatialShapeType.MultiPolygon => $"{name} ({string.Join(separator: ',', values: shape.Children.Select(selector: PolygonBody))})",
            SqlSpatialShapeType.GeometryCollection => $"{name} ({string.Join(separator: ',', values: shape.Children.Select(selector: child => FormatShape(shape: child)))})",
            SqlSpatialShapeType.CompoundCurve or SqlSpatialShapeType.CurvePolygon => $"{name} ({string.Join(separator: ',', values: shape.Children.Select(selector: child => child.Type == SqlSpatialShapeType.LineString ? RingText(line: child) : FormatShape(shape: child)))})",
            _ => throw new InvalidOperationException(message: "Unknown spatial shape.")};
    }

    private static string CoordinatesText(IEnumerable<SqlSpatialCoordinate> points) =>
        string.Join(separator: ',', values: points.Select(selector: point => FormatCoordinate(point: point)));

    private static string RingText(SqlSpatialShape line) =>
        $"({CoordinatesText(points: line.Points)})";

    private static string PolygonBody(SqlSpatialShape polygon) =>
        $"({string.Join(separator: ',', values: polygon.Children.Select(selector: RingText))})";

    public string FormatSqlSpatialShapeGml(SqlSpatialShape sqlSpatialShape, int srid) =>
        TryCatch(operation: () =>
    {
        ValidateSqlSpatialShapeGml(sqlSpatialShape: sqlSpatialShape, srid: srid);
        return FormatGml(shape: sqlSpatialShape, srid: srid, root: true, depth: 0);
    });

    private static string FormatGml(SqlSpatialShape shape, int srid, bool root, int depth)
    {
        if (depth > 128)
        {
            throw new ArgumentException(message: "Spatial GML nesting exceeds 128 levels.", paramName: nameof(shape));
        }

        var name = shape.Type switch
        {
            SqlSpatialShapeType.Point => "Point",
            SqlSpatialShapeType.LineString => "LineString",
            SqlSpatialShapeType.Polygon => "Polygon",
            _ => "MultiGeometry"
        };

        var output = new StringBuilder();

        output.Append(value: '<')
            .Append(value: name)
            .Append(value: " srsName=\"urn:ogc:def:crs:EPSG::")
            .Append(value: srid.ToString(provider: CultureInfo.InvariantCulture))
            .Append(value: '\"');

        if (root)
        {
            output.Append(value: " xmlns=\"http://www.opengis.net/gml\"");
        }

        if (shape.Type is not (SqlSpatialShapeType.Point or SqlSpatialShapeType.LineString) && shape.Children.Length == 0)
        {
            return output.Append(value: " />")
            .ToString();
        }

        output.Append(value: '>');

        switch (shape.Type)
        {
            case SqlSpatialShapeType.Point:
                output.Append(value: "<pos>")
            .Append(value: FormatCoordinate(point: shape.Points.Single()))
            .Append(value: "</pos>");
                break;
            case SqlSpatialShapeType.LineString:
                output.Append(value: "<posList>")
            .Append(value: string.Join(separator: ' ', values: shape.Points.Select(selector: point => FormatCoordinate(point: point))))
            .Append(value: "</posList>");
                break;
            case SqlSpatialShapeType.Polygon:
                for (var i = 0; i < shape.Children.Length; i++)
                {
                    var boundary = i == 0 ? "exterior" : "interior";

                    output.Append(value: '<')
            .Append(value: boundary)
            .Append(value: "><LinearRing><posList>")
            .Append(value: string.Join(separator: ' ', values: shape.Children[i].Points.Select(selector: point => FormatCoordinate(point: point))))
            .Append(value: "</posList></LinearRing></")
            .Append(value: boundary)
            .Append(value: '>');
                }

                break;
            default:
                foreach (var child in shape.Children)
                {
                    output.Append(value: "<geometryMember>")
            .Append(value: FormatGml(shape: child, srid: srid, root: false, depth: depth + 1))
            .Append(value: "</geometryMember>");
                }

                break;
        }

        return output.Append(value: "</")
            .Append(value: name)
            .Append(value: '>')
            .ToString();
    }

    private static string EncodeShape(SqlSpatialShape shape)
    {
        var content = new StringBuilder();
        AppendShape(shape: shape, content: content);
        return content.ToString();
    }

    private static void AppendShape(SqlSpatialShape shape, StringBuilder content)
    {
        content.Append(value: (char)shape.Type)
            .Append(value: shape.IsEmpty ? (char)1 : (char)0);

        AppendBits(content: content, bits: (ulong)shape.Points.Length);

        foreach (var point in shape.Points)
        {
            content.Append(value: (char)((point.Z.HasValue ? 1 : 0) | (point.M.HasValue ? 2 : 0)));
            AppendBits(content: content, bits: BitConverter.DoubleToUInt64Bits(value: point.X));
            AppendBits(content: content, bits: BitConverter.DoubleToUInt64Bits(value: point.Y));
            AppendBits(content: content, bits: BitConverter.DoubleToUInt64Bits(value: point.Z ?? 0));
            AppendBits(content: content, bits: BitConverter.DoubleToUInt64Bits(value: point.M ?? 0));
        }

        AppendBits(content: content, bits: (ulong)shape.Children.Length);

        foreach (var child in shape.Children)
        {
            AppendShape(shape: child, content: content);
        }
    }

    private static void AppendBits(StringBuilder content, ulong bits)
    {
        for (var shift = 0; shift < 64; shift += 16)
        {
            content.Append(value: (char)((bits >> shift) & 65535));
        }
    }

    private static SqlSpatialShape DecodeShape(string content)
    {
        var offset = 0;
        var shape = ReadShapeContent(content: content, offset: ref offset, depth: 0);

        if (offset != content.Length)
        {
            throw new StorageFormatException(message: "Spatial content has trailing data.");
        }

        return shape;
    }

    private static SqlSpatialShape ReadShapeContent(string content, ref int offset, int depth)
    {
        if (depth > 128 || content.Length - offset < 10)
        {
            throw new StorageFormatException(message: "Spatial content is truncated or too deeply nested.");
        }

        var type = (SqlSpatialShapeType)content[offset++];
        var empty = content[offset++];

        if (!Enum.IsDefined(value: type) || empty > 1)
        {
            throw new StorageFormatException(message: "Spatial content metadata is invalid.");
        }

        var count = ReadBits(content: content, offset: ref offset);

        if (count > (ulong)((content.Length - offset - 4) / 17))
        {
            throw new StorageFormatException(message: "Spatial coordinate count is invalid.");
        }

        var points = new SqlSpatialCoordinate[(int)count];

        for (var index = 0; index < points.Length; index++)
        {
            var flags = content[offset++];
            var x = BitConverter.UInt64BitsToDouble(value: ReadBits(content: content, offset: ref offset));
            var y = BitConverter.UInt64BitsToDouble(value: ReadBits(content: content, offset: ref offset));
            var z = BitConverter.UInt64BitsToDouble(value: ReadBits(content: content, offset: ref offset));
            var m = BitConverter.UInt64BitsToDouble(value: ReadBits(content: content, offset: ref offset));

            if (flags > 3 || !double.IsFinite(d: x) || !double.IsFinite(d: y) || !double.IsFinite(d: z) || !double.IsFinite(d: m))
            {
                throw new StorageFormatException(message: "Spatial coordinate is invalid.");
            }

            points[index] = new SqlSpatialCoordinate
            {
                X = x,
                Y = y,
                Z = (flags & 1) != 0 ? z : null,
                M = (flags & 2) != 0 ? m : null
            };
        }

        var childCount = ReadBits(content: content, offset: ref offset);

        if (childCount > (ulong)((content.Length - offset) / 10))
        {
            throw new StorageFormatException(message: "Spatial child count is invalid.");
        }

        var children = new SqlSpatialShape[(int)childCount];

        for (var index = 0; index < children.Length; index++)
        {
            children[index] = ReadShapeContent(content: content, offset: ref offset, depth: depth + 1);
        }

        return new SqlSpatialShape
        {
            Type = type,
            IsEmpty = empty == 1,
            Points = points,
            Children = children
        };
    }

    private static ulong ReadBits(string content, ref int offset)
    {
        if (content.Length - offset < 4)
        {
            throw new StorageFormatException(message: "Spatial component is truncated.");
        }

        ulong bits = 0;

        for (var shift = 0; shift < 64; shift += 16)
        {
            bits |= (ulong)content[offset++] << shift;
        }

        return bits;
    }

    public string WriteSqlSpatialShapeContent(SqlSpatialShape sqlSpatialShape) =>
        TryCatch(operation: () =>
    {
        ValidateSqlSpatialShape(sqlSpatialShape: sqlSpatialShape);
        return EncodeShape(shape: sqlSpatialShape);
    });

    public SqlSpatialShape ReadSqlSpatialShapeContent(string content) =>
        TryCatch(operation: () =>
    {
        ValidateSqlSpatialShapeContentSource(content: content);
        return DecodeShape(content: content);
    });
}