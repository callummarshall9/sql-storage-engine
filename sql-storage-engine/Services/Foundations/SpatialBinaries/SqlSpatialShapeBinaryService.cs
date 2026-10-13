// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers;
using System.Buffers.Binary;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Spatial;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.SpatialBinaries;
internal sealed partial class SqlSpatialShapeBinaryService : ISqlSpatialShapeBinaryService
{
    public byte[] EncodeSqlSpatialShape(SqlSpatialKind kind, int srid, SqlSpatialShape sqlSpatialShape) =>
        TryCatch(operation: () =>
    {
        ValidateSqlSpatialShapeSource(kind: kind, srid: srid, sqlSpatialShape: sqlSpatialShape);
        var output = new ArrayBufferWriter<byte>();
        WriteByte(output: output, value: 2);
        WriteByte(output: output, value: (byte)kind);
        WriteInt(output: output, value: srid);
        WriteShape(output: output, shape: sqlSpatialShape);
        return output.WrittenSpan.ToArray();
    });

    public (SqlSpatialKind Kind, int Srid, SqlSpatialShape Shape) DecodeSqlSpatialShape(ReadOnlySpan<byte> value) =>
        TryCatch(operation: context =>
    {
        ValidateSqlSpatialShapeBytes(value: context.value);

        var cursor = new SqlSpatialBinaryCursor
        {
            Source = context.value
        };

        if (ReadByte(cursor: ref cursor) != 2)
        {
            throw new StorageFormatException(message: "Unsupported spatial binary version.");
        }

        var kind = (SqlSpatialKind)ReadByte(cursor: ref cursor);

        if (!Enum.IsDefined(value: kind))
        {
            throw new StorageFormatException(message: "Invalid spatial kind.");
        }

        var srid = ReadInt(cursor: ref cursor);
        var shape = ReadSqlSpatialShapeCore(cursor: ref cursor, depth: 0);

        if (cursor.Offset != cursor.Source.Length)
        {
            throw new StorageFormatException(message: "Spatial representation contains trailing bytes.");
        }

        return (kind, srid, shape);
    }, context: new ReadableSqlSpatialBinaryContext { value = value });

    private static void WriteShape(IBufferWriter<byte> output, SqlSpatialShape shape)
    {
        WriteByte(output: output, value: (byte)shape.Type);
        WriteByte(output: output, value: shape.IsEmpty ? (byte)1 : (byte)0);
        WriteInt(output: output, value: shape.Points.Length);

        foreach (var point in shape.Points)
        {
            WriteDouble(output: output, value: point.X);
            WriteDouble(output: output, value: point.Y);
            var flags = (byte)((point.Z is null ? 0 : 1) | (point.M is null ? 0 : 2));
            WriteByte(output: output, value: flags);

            if (point.Z is { } z)
            {
                WriteDouble(output: output, value: z);
            }

            if (point.M is { } m)
            {
                WriteDouble(output: output, value: m);
            }
        }

        WriteInt(output: output, value: shape.Children.Length);

        foreach (var child in shape.Children)
        {
            WriteShape(output: output, shape: child);
        }
    }

    private static void WriteByte(IBufferWriter<byte> output, byte value)
    {
        var span = output.GetSpan(sizeHint: 1);
        span[0] = value;
        output.Advance(count: 1);
    }

    private static void WriteInt(IBufferWriter<byte> output, int value)
    {
        var span = output.GetSpan(sizeHint: 4);
        BinaryPrimitives.WriteInt32LittleEndian(destination: span, value: value);
        output.Advance(count: 4);
    }

    private static void WriteDouble(IBufferWriter<byte> output, double value)
    {
        var span = output.GetSpan(sizeHint: 8);
        BinaryPrimitives.WriteDoubleLittleEndian(destination: span, value: value);
        output.Advance(count: 8);
    }

    private static byte ReadByte(ref SqlSpatialBinaryCursor cursor)
    {
        Require(cursor: cursor, count: 1);
        return cursor.Source[cursor.Offset++];
    }

    private static int ReadInt(ref SqlSpatialBinaryCursor cursor)
    {
        Require(cursor: cursor, count: 4);
        var value = BinaryPrimitives.ReadInt32LittleEndian(source: cursor.Source[cursor.Offset..]);
        cursor.Offset += 4;
        return value;
    }

    private static double ReadDouble(ref SqlSpatialBinaryCursor cursor)
    {
        Require(cursor: cursor, count: 8);
        var value = BinaryPrimitives.ReadDoubleLittleEndian(source: cursor.Source[cursor.Offset..]);
        cursor.Offset += 8;

        if (!double.IsFinite(d: value))
        {
            throw new StorageFormatException("Spatial coordinate is not finite.");
        }

        return value;
    }

    private static SqlSpatialShape ReadSqlSpatialShapeCore(ref SqlSpatialBinaryCursor cursor, int depth)
    {
        if (depth > 128)
        {
            throw new StorageFormatException(message: "Spatial representation exceeds the supported nesting depth.");
        }

        if (++cursor.Nodes > 1_000_000)
        {
            throw new StorageFormatException("Spatial representation has too many nodes.");
        }

        var type = (SqlSpatialShapeType)ReadByte(cursor: ref cursor);

        if (!Enum.IsDefined(value: type))
        {
            throw new StorageFormatException("Unknown spatial shape type.");
        }

        var empty = ReadByte(cursor: ref cursor) switch
        {
            0 => false,
            1 => true,
            _ => throw new StorageFormatException("Invalid spatial empty marker.")};

        var pointCount = ReadCount(cursor: ref cursor);
        var points = new SqlSpatialCoordinate[pointCount];

        for (var index = 0; index < points.Length; index++)
        {
            var x = ReadDouble(cursor: ref cursor);
            var y = ReadDouble(cursor: ref cursor);
            var flags = ReadByte(cursor: ref cursor);

            if (flags > 3)
            {
                throw new StorageFormatException("Invalid spatial coordinate flags.");
            }

            points[index] = new sql_storage_engine.Models.Spatial.SqlSpatialCoordinate
            {
                Z = (flags & 1) != 0 ? ReadDouble(cursor: ref cursor) : null,
                M = (flags & 2) != 0 ? ReadDouble(cursor: ref cursor) : null,
                X = x,
                Y = y
            };
        }

        var childCount = ReadCount(cursor: ref cursor);
        var children = new SqlSpatialShape[childCount];

        for (var index = 0; index < children.Length; index++)
        {
            children[index] = ReadSqlSpatialShapeCore(cursor: ref cursor, depth: depth + 1);
        }

        return new SqlSpatialShape
        {
            Points = points,
            Children = children,
            IsEmpty = empty,
            Type = type
        };
    }

    private static int ReadCount(ref SqlSpatialBinaryCursor cursor)
    {
        var value = ReadInt(cursor: ref cursor);

        if (value is < 0 or > 1_000_000)
        {
            throw new StorageFormatException("Invalid spatial collection count.");
        }

        return value;
    }

    private static void Require(SqlSpatialBinaryCursor cursor, int count)
    {
        if (cursor.Offset > cursor.Source.Length - count)
        {
            throw new StorageFormatException("Spatial representation is truncated.");
        }
    }
}