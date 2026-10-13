// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Numerics;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Buffers;
using System.Buffers.Binary;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.SqlHierarchies;
internal sealed partial class SqlHierarchyIdService : ISqlHierarchyIdService
{
    public SqlHierarchyId ReadRootSqlHierarchyId() =>
        TryCatch(operation: () =>
    {
        ValidateRoot();

        return new SqlHierarchyId
        {
            Path = "/"
        };
    });

    public SqlHierarchyId ParseSqlHierarchyId(string value) =>
        TryCatch(operation: () =>
    {
        ValidateText(value: value);
        return CreateSqlHierarchyId(labels: ParseLabels(value: value));
    });

    public int ReadSqlHierarchyIdLevel(SqlHierarchyId sqlHierarchyId) =>
        TryCatch(operation: () =>
    {
        ValidateSqlHierarchyId(sqlHierarchyId: sqlHierarchyId);

        return ParseLabels(value: sqlHierarchyId.Path)
            .Length;
    });

    public string FormatSqlHierarchyId(SqlHierarchyId sqlHierarchyId) =>
        TryCatch(operation: () =>
    {
        ValidateSqlHierarchyId(sqlHierarchyId: sqlHierarchyId);
        return sqlHierarchyId.Path;
    });

    public SqlHierarchyId? ReadSqlHierarchyIdAncestor(SqlHierarchyId sqlHierarchyId, int levels) =>
        TryCatch(operation: () =>
    {
        ValidateAncestor(sqlHierarchyId: sqlHierarchyId, levels: levels);
        var labels = ParseLabels(value: sqlHierarchyId.Path);
        return levels > labels.Length ? null : CreateSqlHierarchyId(labels: labels.Take(count: labels.Length - levels));
    });

    public bool CheckSqlHierarchyIdDescendant(SqlHierarchyId[] sqlHierarchyIds) =>
        TryCatch(operation: () =>
    {
        ValidatePair(sqlHierarchyIds: sqlHierarchyIds);
        return Descendant(labels: ParseLabels(value: sqlHierarchyIds[0].Path), parent: ParseLabels(value: sqlHierarchyIds[1].Path));
    });

    public SqlHierarchyId ReparentSqlHierarchyId(SqlHierarchyId[] sqlHierarchyIds) =>
        TryCatch(operation: () =>
    {
        ValidateTriple(sqlHierarchyIds: sqlHierarchyIds);
        var labels = ParseLabels(value: sqlHierarchyIds[0].Path);
        var oldRoot = ParseLabels(value: sqlHierarchyIds[1].Path);
        var newRoot = ParseLabels(value: sqlHierarchyIds[2].Path);

        if (!Descendant(labels: labels, parent: oldRoot))
        {
            throw new ArgumentException(message: "The value is not below oldRoot.", paramName: "oldRoot");
        }

        return CreateSqlHierarchyId(labels: newRoot.Concat(second: labels.Skip(count: oldRoot.Length)));
    });

    public SqlHierarchyId CreateSqlHierarchyIdDescendant(SqlHierarchyId? [] sqlHierarchyIds) =>
        TryCatch(operation: () =>
    {
        ValidateChildren(sqlHierarchyIds: sqlHierarchyIds);
        var labels = ParseLabels(value: sqlHierarchyIds[0]!.Path);
        var left = sqlHierarchyIds[1] is null ? null : ParseLabels(value: sqlHierarchyIds[1]!.Path);
        var right = sqlHierarchyIds[2] is null ? null : ParseLabels(value: sqlHierarchyIds[2]!.Path);

        if (left != null && (left.Length != labels.Length + 1 || !Descendant(labels: left, parent: labels)))
        {
            throw new ArgumentException(message: "left must be an immediate child.", paramName: nameof(left));
        }

        if (right != null && (right.Length != labels.Length + 1 || !Descendant(labels: right, parent: labels)))
        {
            throw new ArgumentException(message: "right must be an immediate child.", paramName: nameof(right));
        }

        var leftLabel = left?[^1];
        var rightLabel = right?[^1];

        if (leftLabel != null && rightLabel != null && CompareLabel(left: leftLabel, right: rightLabel) >= 0)
        {
            throw new ArgumentException(message: "left must sort before right.");
        }

        BigInteger[] label;

        if (leftLabel is null && rightLabel is null)
        {
            label = [BigInteger.One];
        }
        else if (leftLabel is null)
        {
            label = [rightLabel![0] - BigInteger.One];
        }
        else if (rightLabel is null)
        {
            label = [leftLabel[0] + BigInteger.One];
        }
        else if (IsPrefix(prefix: leftLabel, value: rightLabel))
        {
            label = leftLabel.Append(element: rightLabel[leftLabel.Length] - BigInteger.One)
            .ToArray();
        }
        else
        {
            label = leftLabel.Append(element: BigInteger.One)
            .ToArray();
        }

        return CreateSqlHierarchyId(labels: labels.Append(element: label));
    });

    public int CompareSqlHierarchyIds(SqlHierarchyId? [] sqlHierarchyIds) =>
        TryCatch(operation: () =>
    {
        ValidateNullablePair(sqlHierarchyIds: sqlHierarchyIds);

        if (sqlHierarchyIds[1] is null)
        {
            return 1;
        }

        var labels = ParseLabels(value: sqlHierarchyIds[0]!.Path);
        var other = ParseLabels(value: sqlHierarchyIds[1]!.Path);

        for (var index = 0; index < Math.Min(val1: labels.Length, val2: other.Length); index++)
        {
            var result = CompareLabel(left: labels[index], right: other[index]);

            if (result != 0)
            {
                return result;
            }
        }

        return labels.Length.CompareTo(value: other.Length);
    });

    public byte[] WriteSqlHierarchyId(SqlHierarchyId sqlHierarchyId) =>
        TryCatch(operation: () =>
    {
        ValidateSqlHierarchyId(sqlHierarchyId: sqlHierarchyId);
        var output = new ArrayBufferWriter<byte>();
        var labels = ParseLabels(value: sqlHierarchyId.Path);
        WriteByte(output: output, value: 2);
        WriteVarUInt(output: output, value: checked((uint)labels.Length));

        foreach (var label in labels)
        {
            WriteVarUInt(output: output, value: checked((uint)label.Length));

            foreach (var component in label)
            {
                WriteInteger(output: output, value: component);
            }
        }

        if (output.WrittenCount > 892)
        {
            throw new InvalidOperationException(message: "The hierarchyid representation exceeds 892 bytes.");
        }

        return output.WrittenSpan.ToArray();
    });

    public byte[] ReadSqlHierarchyIdSortKey(SqlHierarchyId sqlHierarchyId) =>
        TryCatch(operation: () =>
    {
        ValidateSqlHierarchyId(sqlHierarchyId: sqlHierarchyId);
        var output = new ArrayBufferWriter<byte>();

        foreach (var label in ParseLabels(value: sqlHierarchyId.Path))
        {
            foreach (var component in label)
            {
                WriteSortableInteger(output: output, value: component);
            }

            WriteByte(output: output, value: 0);
        }

        return output.WrittenSpan.ToArray();
    });

    public SqlHierarchyId ReadSqlHierarchyId(byte[] value) =>
        TryCatch(operation: () =>
    {
        ValidateBinary(value: value);
        var offset = 1;
        var count = ReadVarUInt(source: value, offset: ref offset);

        if (count > value.Length - offset)
        {
            throw new StorageFormatException(message: "Hierarchyid contains an invalid level count.");
        }

        var labels = new BigInteger[checked((int)count)][];

        for (var level = 0; level < labels.Length; level++)
        {
            var components = ReadVarUInt(source: value, offset: ref offset);

            if (components == 0 || components > value.Length - offset)
            {
                throw new StorageFormatException(message: "A hierarchyid label has an invalid component count.");
            }

            labels[level] = new BigInteger[checked((int)components)];

            for (var component = 0; component < labels[level].Length; component++)
            {
                labels[level][component] = ReadInteger(source: value, offset: ref offset);
            }
        }

        if (offset != value.Length)
        {
            throw new StorageFormatException(message: "Hierarchyid contains trailing bytes.");
        }

        return CreateSqlHierarchyId(labels: labels);
    });

    private static BigInteger[][] ParseLabels(string value)
    {
        ArgumentNullException.ThrowIfNull(argument: value);

        if (value == "/")
        {
            return[];
        }

        if (value.Length < 3 || value[0] != '/' || value[^1] != '/')
        {
            throw new FormatException("A hierarchyid path must begin and end with '/'.");
        }

        var parts = value[1..^1].Split(separator: '/');
        var labels = new BigInteger[parts.Length][];

        for (var level = 0; level < parts.Length; level++)
        {
            var components = parts[level].Split(separator: '.');

            if (components.Length == 0)
            {
                throw new FormatException("A hierarchyid label cannot be empty.");
            }

            labels[level] = new BigInteger[components.Length];

            for (var component = 0; component < components.Length; component++)
            {
                var token = components[component];

                if (!BigInteger.TryParse(value: token, style: NumberStyles.AllowLeadingSign, provider: CultureInfo.InvariantCulture, result: out labels[level][component]) || !StringComparer.Ordinal.Equals(x: token, y: labels[level][component].ToString(provider: CultureInfo.InvariantCulture)))
                {
                    throw new FormatException($"Invalid hierarchyid integer '{token}'.");
                }
            }
        }

        return labels;
    }

    private static SqlHierarchyId CreateSqlHierarchyId(IEnumerable<BigInteger[]> labels)
    {
        var materialized = labels.ToArray();

        return new SqlHierarchyId
        {
            Path = materialized.Length == 0 ? "/" : "/" + string.Join(separator: '/', values: materialized.Select(selector: label => string.Join(separator: '.', values: label.Select(selector: component => component.ToString(provider: CultureInfo.InvariantCulture))))) + "/"
        };
    }

    private static bool Descendant(BigInteger[][] labels, BigInteger[][] parent)
    {
        if (parent.Length > labels.Length)
        {
            return false;
        }

        for (var level = 0; level < parent.Length; level++)
        {
            if (!parent[level].AsSpan()
            .SequenceEqual(other: labels[level]))
            {
                return false;
            }
        }

        return true;
    }

    private static byte ReadByte(byte[] source, ref int offset)
    {
        if (offset >= source.Length)
        {
            throw new StorageFormatException(message: "Hierarchyid is truncated.");
        }

        return source[offset++];
    }

    private static uint ReadVarUInt(byte[] source, ref int offset)
    {
        uint value = 0;
        var shift = 0;

        for (var count = 0; count < 5; count++)
        {
            var next = ReadByte(source: source, offset: ref offset);

            if (count == 4 && (next & 0xf0) != 0)
            {
                throw new StorageFormatException(message: "Hierarchyid contains an invalid variable integer.");
            }

            value |= (uint)(next & 127) << shift;

            if ((next & 128) == 0)
            {
                return value;
            }

            shift += 7;
        }

        throw new StorageFormatException(message: "Hierarchyid contains an invalid variable integer.");
    }

    private static BigInteger ReadInteger(byte[] source, ref int offset)
    {
        var sign = ReadByte(source: source, offset: ref offset);

        if (sign > 2)
        {
            throw new StorageFormatException(message: "Hierarchyid contains an invalid integer sign.");
        }

        var length = ReadVarUInt(source: source, offset: ref offset);

        if (length > 892 || length > source.Length - offset)
        {
            throw new StorageFormatException(message: "Hierarchyid is truncated.");
        }

        var magnitude = new BigInteger(source.AsSpan(start: offset, length: checked((int)length)), isUnsigned: true, isBigEndian: true);
        offset += checked((int)length);

        if ((sign == 1) != magnitude.IsZero)
        {
            throw new StorageFormatException(message: "Hierarchyid integer is not canonically encoded.");
        }

        return sign == 0 ? -magnitude : magnitude;
    }

    private static int CompareLabel(ReadOnlySpan<BigInteger> left, ReadOnlySpan<BigInteger> right)
    {
        for (var component = 0; component < Math.Min(val1: left.Length, val2: right.Length); component++)
        {
            var result = left[component].CompareTo(other: right[component]);

            if (result != 0)
            {
                return result;
            }
        }

        return left.Length.CompareTo(value: right.Length);
    }

    private static bool IsPrefix(ReadOnlySpan<BigInteger> prefix, ReadOnlySpan<BigInteger> value) =>
        prefix.Length < value.Length && prefix.SequenceEqual(other: value[..prefix.Length]);

    private static void WriteInteger(IBufferWriter<byte> output, BigInteger value)
    {
        WriteByte(output: output, value: value.Sign switch
        {
            < 0 => (byte)0,
            0 => (byte)1,
            _ => (byte)2
        });

        var magnitude = BigInteger.Abs(value: value)
            .ToByteArray(isUnsigned: true, isBigEndian: true);

        WriteVarUInt(output: output, value: checked((uint)magnitude.Length));
        Write(output: output, value: magnitude);
    }

    private static void WriteSortableInteger(IBufferWriter<byte> output, BigInteger value)
    {
        var magnitude = BigInteger.Abs(value: value)
            .ToByteArray(isUnsigned: true, isBigEndian: true);

        if (magnitude.Length > ushort.MaxValue)
        {
            throw new InvalidOperationException("Hierarchyid integer is too large.");
        }

        Span<byte> length = stackalloc byte[2];

        if (value.Sign < 0)
        {
            WriteByte(output: output, value: 0x10);
            BinaryPrimitives.WriteUInt16BigEndian(destination: length, value: checked((ushort)(ushort.MaxValue - magnitude.Length)));
            Write(output: output, value: length);

            foreach (var item in magnitude)
            {
                WriteByte(output: output, value: (byte)~item);
            }
        }
        else
        {
            WriteByte(output: output, value: 0x20);
            BinaryPrimitives.WriteUInt16BigEndian(destination: length, value: checked((ushort)magnitude.Length));
            Write(output: output, value: length);
            Write(output: output, value: magnitude);
        }
    }

    private static void WriteVarUInt(IBufferWriter<byte> output, uint value)
    {
        while (value >= 0x80)
        {
            WriteByte(output: output, value: (byte)(value | 0x80));
            value >>= 7;
        }

        WriteByte(output: output, value: (byte)value);
    }

    private static void WriteByte(IBufferWriter<byte> output, byte value)
    {
        var span = output.GetSpan(sizeHint: 1);
        span[0] = value;
        output.Advance(count: 1);
    }

    private static void Write(IBufferWriter<byte> output, ReadOnlySpan<byte> value)
    {
        value.CopyTo(destination: output.GetSpan(sizeHint: value.Length));
        output.Advance(count: value.Length);
    }
}