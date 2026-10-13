// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Buffers.Binary;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Spatial;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.SpatialWkbs;
internal sealed partial class SqlSpatialShapeWkbProcessingService : ISqlSpatialShapeWkbProcessingService
{
    public byte[] EncodeSqlSpatialShapeWkb(SqlSpatialShape sqlSpatialShape) =>
        TryCatch(operation: () =>
    {
        ValidateSqlSpatialShapeWkb(sqlSpatialShape: sqlSpatialShape);
        var output = new List<byte>();
        Write(output: output, shape: sqlSpatialShape, depth: 0);
        return output.ToArray();
    });

    public SqlSpatialShape DecodeSqlSpatialShapeWkb(ReadOnlySpan<byte> value) =>
        TryCatch(operation: context =>
    {
        ValidateSqlSpatialShapeWkbSource(value: context.value);

        var cursor = new SqlSpatialBinaryCursor
        {
            Source = context.value
        };

        var shape = Read(cursor: ref cursor, depth: 0);

        if (cursor.Offset != cursor.Source.Length)
        {
            throw new StorageFormatException(message: "WKB contains trailing bytes.");
        }

        return shape;
    }, context: new ReadableSqlSpatialBinaryContext { value = value });

    private static void Write(List<byte> output, SqlSpatialShape shape, int depth)
    {
        ValidateDepth(depth: depth);
        output.Add(item: 1);

        WriteUInt32(output: output, value: shape.Type switch
        {
            SqlSpatialShapeType.Point => 1u,
            SqlSpatialShapeType.LineString => 2u,
            SqlSpatialShapeType.Polygon => 3u,
            SqlSpatialShapeType.MultiPoint => 4u,
            SqlSpatialShapeType.MultiLineString => 5u,
            SqlSpatialShapeType.MultiPolygon => 6u,
            SqlSpatialShapeType.GeometryCollection => 7u,
            _ => throw new NotSupportedException(message: "OGC WKB does not represent SQL curve or FULLGLOBE values.")});

        switch (shape.Type)
        {
            case SqlSpatialShapeType.Point:
                var point = shape.Points.Single();
                WriteDouble(output: output, value: point.X);
                WriteDouble(output: output, value: point.Y);
                break;
            case SqlSpatialShapeType.LineString:
                WritePoints(output: output, points: shape.Points);
                break;
            case SqlSpatialShapeType.Polygon:
                WriteUInt32(output: output, value: (uint)shape.Children.Length);
                foreach (var ring in shape.Children)
                {
                    WritePoints(output: output, points: ring.Points);
                }

                break;
            default:
                WriteUInt32(output: output, value: (uint)shape.Children.Length);
                foreach (var child in shape.Children)
                {
                    Write(output: output, shape: child, depth: depth + 1);
                }

                break;
        }
    }

    private static void WritePoints(List<byte> output, SqlSpatialCoordinate[] points)
    {
        WriteUInt32(output: output, value: (uint)points.Length);

        foreach (var point in points)
        {
            WriteDouble(output: output, value: point.X);
            WriteDouble(output: output, value: point.Y);
        }
    }

    private static void WriteUInt32(List<byte> output, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(destination: bytes, value: value);

        foreach (var item in bytes)
        {
            output.Add(item: item);
        }
    }

    private static void WriteDouble(List<byte> output, double value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteDoubleLittleEndian(destination: bytes, value: value);

        foreach (var item in bytes)
        {
            output.Add(item: item);
        }
    }

    private static SqlSpatialShape Read(ref SqlSpatialBinaryCursor cursor, int depth)
    {
        ValidateDepth(depth: depth);

        var little = ReadByte(cursor: ref cursor) switch
        {
            0 => false,
            1 => true,
            _ => throw new StorageFormatException(message: "Invalid WKB byte order.")};

        var type = ReadUInt32(cursor: ref cursor, little: little);

        switch (type)
        {
            case 1:
                return CreateSqlSpatialShape(type: SqlSpatialShapeType.Point, points: [ReadPoint(cursor: ref cursor, little: little)], children: Array.Empty<SqlSpatialShape>());
            case 2:
                return CreateSqlSpatialShape(type: SqlSpatialShapeType.LineString, points: ReadPoints(cursor: ref cursor, little: little), children: Array.Empty<SqlSpatialShape>());
            case 3:
                var rings = new SqlSpatialShape[ReadCount(cursor: ref cursor, little: little)];
                for (var index = 0; index < rings.Length; index++)
                {
                    rings[index] = CreateSqlSpatialShape(type: SqlSpatialShapeType.LineString, points: ReadPoints(cursor: ref cursor, little: little), children: Array.Empty<SqlSpatialShape>());
                }

                return CreateSqlSpatialShape(type: SqlSpatialShapeType.Polygon, points: Array.Empty<SqlSpatialCoordinate>(), children: rings);
            case 4:
                return CreateSqlSpatialShape(type: SqlSpatialShapeType.MultiPoint, points: Array.Empty<SqlSpatialCoordinate>(), children: ReadChildren(cursor: ref cursor, little: little, expected: SqlSpatialShapeType.Point, depth: depth));
            case 5:
                return CreateSqlSpatialShape(type: SqlSpatialShapeType.MultiLineString, points: Array.Empty<SqlSpatialCoordinate>(), children: ReadChildren(cursor: ref cursor, little: little, expected: SqlSpatialShapeType.LineString, depth: depth));
            case 6:
                return CreateSqlSpatialShape(type: SqlSpatialShapeType.MultiPolygon, points: Array.Empty<SqlSpatialCoordinate>(), children: ReadChildren(cursor: ref cursor, little: little, expected: SqlSpatialShapeType.Polygon, depth: depth));
            case 7:
                return CreateSqlSpatialShape(type: SqlSpatialShapeType.GeometryCollection, points: Array.Empty<SqlSpatialCoordinate>(), children: ReadChildren(cursor: ref cursor, little: little, expected: null, depth: depth));
            default:
                throw new StorageFormatException(message: $"Unsupported WKB geometry type {type}.");
        }
    }

    private static SqlSpatialShape CreateSqlSpatialShape(SqlSpatialShapeType type, SqlSpatialCoordinate[] points, SqlSpatialShape[] children) =>
        new SqlSpatialShape
    {
        Type = type,
        Points = points,
        Children = children
    };

    private static SqlSpatialShape[] ReadChildren(ref SqlSpatialBinaryCursor cursor, bool little, SqlSpatialShapeType? expected, int depth)
    {
        var children = new SqlSpatialShape[ReadCount(cursor: ref cursor, little: little)];

        for (var index = 0; index < children.Length; index++)
        {
            children[index] = Read(cursor: ref cursor, depth: depth + 1);

            if (expected is { } type && children[index].Type != type)
            {
                throw new StorageFormatException(message: "WKB collection contains the wrong shape type.");
            }
        }

        return children;
    }

    private static SqlSpatialCoordinate[] ReadPoints(ref SqlSpatialBinaryCursor cursor, bool little)
    {
        var points = new SqlSpatialCoordinate[ReadCount(cursor: ref cursor, little: little)];

        for (var index = 0; index < points.Length; index++)
        {
            points[index] = ReadPoint(cursor: ref cursor, little: little);
        }

        return points;
    }

    private static SqlSpatialCoordinate ReadPoint(ref SqlSpatialBinaryCursor cursor, bool little) =>
        new SqlSpatialCoordinate
    {
        X = ReadDouble(cursor: ref cursor, little: little),
        Y = ReadDouble(cursor: ref cursor, little: little)
    };

    private static int ReadCount(ref SqlSpatialBinaryCursor cursor, bool little)
    {
        var value = ReadUInt32(cursor: ref cursor, little: little);

        if (value > 1_000_000)
        {
            throw new StorageFormatException(message: "WKB collection is too large.");
        }

        return (int)value;
    }

    private static byte ReadByte(ref SqlSpatialBinaryCursor cursor)
    {
        Require(cursor: cursor, count: 1);
        return cursor.Source[cursor.Offset++];
    }

    private static uint ReadUInt32(ref SqlSpatialBinaryCursor cursor, bool little)
    {
        Require(cursor: cursor, count: 4);
        var bytes = cursor.Source.Slice(start: cursor.Offset, length: 4);
        cursor.Offset += 4;
        return little ? BinaryPrimitives.ReadUInt32LittleEndian(source: bytes) : BinaryPrimitives.ReadUInt32BigEndian(source: bytes);
    }

    private static double ReadDouble(ref SqlSpatialBinaryCursor cursor, bool little)
    {
        Require(cursor: cursor, count: 8);
        var bytes = cursor.Source.Slice(start: cursor.Offset, length: 8);
        cursor.Offset += 8;
        var value = little ? BinaryPrimitives.ReadDoubleLittleEndian(source: bytes) : BinaryPrimitives.ReadDoubleBigEndian(source: bytes);

        if (!double.IsFinite(d: value))
        {
            throw new StorageFormatException(message: "WKB coordinate is not finite.");
        }

        return value;
    }

    private static void Require(SqlSpatialBinaryCursor cursor, int count)
    {
        if (cursor.Offset > cursor.Source.Length - count)
        {
            throw new StorageFormatException(message: "WKB is truncated.");
        }
    }
}