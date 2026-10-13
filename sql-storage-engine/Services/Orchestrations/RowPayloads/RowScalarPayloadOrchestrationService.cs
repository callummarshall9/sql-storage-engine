// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers.Binary;
using System.Linq;
using System.Collections.Generic;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Services.Foundations.SqlDecimals;
using sql_storage_engine.Services.Foundations.SqlHierarchies;
using sql_storage_engine.Services.Foundations.ColumnEncryption;

namespace sql_storage_engine.Services.Orchestrations.RowPayloads;
internal sealed partial class RowScalarPayloadOrchestrationService : IRowScalarPayloadOrchestrationService
{
    private readonly SqlDecimalService decimals;
    private readonly SqlHierarchyIdService hierarchies;
    private readonly CatalogColumnEncryptionService encryption;
    public RowScalarPayloadOrchestrationService(SqlDecimalService decimals, SqlHierarchyIdService hierarchies, CatalogColumnEncryptionService encryption)
    {
        this.decimals = decimals;
        this.hierarchies = hierarchies;
        this.encryption = encryption;
    }

    public RowPayloadContext ReadRowPayloadContextWidth(RowPayloadContext rowPayloadContext) =>
        TryCatch(operation: () =>
    {
        ValidateContext(rowPayloadContext: rowPayloadContext);
        rowPayloadContext.Width = GetFixedWidth(column: rowPayloadContext.Column!);
        return rowPayloadContext;
    });

    public RowPayloadContext WriteRowPayloadContextFixed(RowPayloadContext rowPayloadContext) =>
        TryCatch(operation: () =>
    {
        ValidateValue(rowPayloadContext: rowPayloadContext);
        rowPayloadContext.Bytes = new byte[GetFixedWidth(column: rowPayloadContext.Column!)];
        WriteFixedValue(destination: rowPayloadContext.Bytes, value: rowPayloadContext.Value!, column: rowPayloadContext.Column!);
        return rowPayloadContext;
    });

    public RowPayloadContext ReadRowPayloadContextFixed(RowPayloadContext rowPayloadContext) =>
        TryCatch(operation: () =>
    {
        ValidateSource(rowPayloadContext: rowPayloadContext);
        rowPayloadContext.Value = DecodeFixedValue(source: rowPayloadContext.Source!, column: rowPayloadContext.Column!);
        return rowPayloadContext;
    });

    public RowPayloadContext WriteRowPayloadContextScalar(RowPayloadContext rowPayloadContext) =>
        TryCatch(operation: () =>
    {
        ValidateValue(rowPayloadContext: rowPayloadContext);

        rowPayloadContext.Bytes = rowPayloadContext.Value switch
        {
            BinarySqlValue binary => EncodeBinary(value: ReadBinarySqlValue(binarySqlValue: binary)
            .Span, type: rowPayloadContext.Column!.Type),
            HierarchyIdSqlValue hierarchy => hierarchies.WriteSqlHierarchyId(sqlHierarchyId: hierarchy.Value),
            VectorSqlValue vector => EncodeVector(vector: vector),
            _ => EncodeFixedForEncryption(value: rowPayloadContext.Value!, column: rowPayloadContext.Column!)};

        return rowPayloadContext;
    });

    public RowPayloadContext ReadRowPayloadContextScalar(RowPayloadContext rowPayloadContext) =>
        TryCatch(operation: () =>
    {
        ValidateSource(rowPayloadContext: rowPayloadContext);

        rowPayloadContext.Value = rowPayloadContext.Column!.Type.Name switch
        {
            SqlTypeName.HierarchyId => new HierarchyIdSqlValue
            {
                Value = hierarchies.ReadSqlHierarchyId(value: rowPayloadContext.Source!),
                StorageFamily = SqlStorageFamily.Binary
            },
            SqlTypeName.Vector => DecodeVector(source: rowPayloadContext.Source!, type: rowPayloadContext.Column.Type),
            _ => CreateBinarySqlValue(value: rowPayloadContext.Source!)};

        return rowPayloadContext;
    });

    public RowPayloadContext EncryptRowPayloadContext(RowPayloadContext rowPayloadContext) =>
        TryCatch(operation: () =>
    {
        ValidateSource(rowPayloadContext: rowPayloadContext);
        ValidateEncryption(rowPayloadContext: rowPayloadContext);
        rowPayloadContext.Bytes = encryption.EncryptCatalogColumnEncryptionPayload(catalogColumnEncryption: rowPayloadContext.Column!.Encryption!, payload: rowPayloadContext.Source!);
        return rowPayloadContext;
    });

    public RowPayloadContext DecryptRowPayloadContext(RowPayloadContext rowPayloadContext) =>
        TryCatch(operation: () =>
    {
        ValidateSource(rowPayloadContext: rowPayloadContext);
        ValidateEncryption(rowPayloadContext: rowPayloadContext);
        rowPayloadContext.Bytes = encryption.DecryptCatalogColumnEncryptionPayload(catalogColumnEncryption: rowPayloadContext.Column!.Encryption!, payload: rowPayloadContext.Source!);
        return rowPayloadContext;
    });

    private int GetFixedWidth(ColumnDefinition column) =>
        column.IsSparse || column.IsColumnSet || column.Encryption is not null ? 0 : column.Type.Name switch
    {
        SqlTypeName.Bit or SqlTypeName.TinyInt => 1,
        SqlTypeName.SmallInt => 2,
        SqlTypeName.Int or SqlTypeName.SmallMoney or SqlTypeName.Real or SqlTypeName.Date => 4,
        SqlTypeName.Float when column.Type.Precision <= 24 => 4,
        SqlTypeName.BigInt or SqlTypeName.Money or SqlTypeName.Float or SqlTypeName.Time or SqlTypeName.SmallDateTime or SqlTypeName.DateTime or SqlTypeName.DateTime2 => 8,
        SqlTypeName.Decimal or SqlTypeName.Numeric => GetDecimalWidth(type: column.Type),
        SqlTypeName.DateTimeOffset => 10,
        SqlTypeName.RowVersion or SqlTypeName.Timestamp => 8,
        SqlTypeName.UniqueIdentifier => 16,
        _ => 0
    };

    private void WriteFixedValue(Span<byte> destination, SqlValue value, ColumnDefinition column)
    {
        switch (value)
        {
            case BooleanSqlValue boolean:
                destination[0] = boolean.Value ? (byte)1 : (byte)0;
                break;
            case IntegerSqlValue integer:
                WriteInteger(destination: destination, value: integer.Value, type: column.Type.Name);
                break;
            case DecimalSqlValue exact:
                WriteDecimal(destination: destination, value: exact.Value, type: column.Type);
                break;
            case FloatSqlValue approximate when column.Type.Name == SqlTypeName.Real || column.Type.Name == SqlTypeName.Float && column.Type.Precision <= 24:
                BinaryPrimitives.WriteSingleLittleEndian(destination: destination, value: (float)approximate.Value);
                break;
            case FloatSqlValue approximate:
                BinaryPrimitives.WriteDoubleLittleEndian(destination: destination, value: approximate.Value);
                break;
            case DateSqlValue date:
                BinaryPrimitives.WriteInt32LittleEndian(destination: destination, value: date.Value.DayNumber);
                break;
            case TimeSqlValue time:
                BinaryPrimitives.WriteInt64LittleEndian(destination: destination, value: RoundTemporalTicks(ticks: time.Value.Ticks, scale: column.Type.Scale!.Value) % TimeSpan.TicksPerDay);
                break;
            case DateTimeSqlValue dateTime:
                BinaryPrimitives.WriteInt64LittleEndian(destination: destination, value: RoundDateTime(value: dateTime.Value, type: column.Type)
            .Ticks);
                break;
            case DateTimeOffsetSqlValue dateTimeOffset:
                BinaryPrimitives.WriteInt64LittleEndian(destination: destination, value: RoundTemporalTicks(ticks: dateTimeOffset.Value.UtcTicks, scale: column.Type.Scale!.Value));
                BinaryPrimitives.WriteInt16LittleEndian(destination: destination[8..], value: checked((short)dateTimeOffset.Value.Offset.TotalMinutes));
                break;
            case BinarySqlValue binary when column.Type.Name is SqlTypeName.RowVersion or SqlTypeName.Timestamp:
                ReadBinarySqlValue(binarySqlValue: binary)
            .Span.CopyTo(destination: destination);
                break;
            case UniqueIdentifierSqlValue identifier:
                if (!identifier.Value.TryWriteBytes(destination: destination, bigEndian: true, bytesWritten: out var bytesWritten) || bytesWritten != 16)
                {
                    throw new InvalidOperationException("Could not encode SQL unique identifier.");
                }

                break;
            default:
                throw new ArgumentException("Value is not a supported fixed-width SQL value.", nameof(value));
        }
    }

    private long RoundTemporalTicks(long ticks, byte scale)
    {
        var quantum = (long)Math.Pow(x: 10, y: 7 - scale);
        var remainder = ticks % quantum;

        if (remainder == 0)
        {
            return ticks;
        }

        return checked(ticks - remainder + (remainder * 2 >= quantum ? quantum : 0));
    }

    private DateTime RoundDateTime(DateTime value, SqlType type)
    {
        if (type.Name == SqlTypeName.SmallDateTime)
        {
            var minuteTicks = value.Ticks - value.Ticks % TimeSpan.TicksPerMinute;

            if (value.Ticks - minuteTicks >= TimeSpan.TicksPerSecond * 30)
            {
                minuteTicks += TimeSpan.TicksPerMinute;
            }

            return new DateTime(minuteTicks, DateTimeKind.Unspecified);
        }

        if (type.Name == SqlTypeName.DateTime)
        {
            var dayTicks = value.Date.Ticks;
            var units = (long)Math.Round(value: value.TimeOfDay.TotalSeconds * 300d, mode: MidpointRounding.AwayFromZero);
            var rounded = checked(dayTicks + (long)Math.Round(value: units * (double)TimeSpan.TicksPerSecond / 300d, mode: MidpointRounding.AwayFromZero));
            return new DateTime(rounded, DateTimeKind.Unspecified);
        }

        return new DateTime(RoundTemporalTicks(ticks: value.Ticks, scale: type.Scale!.Value), DateTimeKind.Unspecified);
    }

    private SqlValue DecodeFixedValue(ReadOnlySpan<byte> source, ColumnDefinition column)
    {
        try
        {
            return column.Type.Name switch
            {
                SqlTypeName.Bit => source[0] switch
                {
                    0 => new BooleanSqlValue
                    {
                        Value = false,
                        StorageFamily = SqlStorageFamily.Boolean
                    },
                    1 => new BooleanSqlValue
                    {
                        Value = true,
                        StorageFamily = SqlStorageFamily.Boolean
                    },
                    _ => throw new StorageFormatException($"Invalid boolean byte for column '{column.Name}'.")},
                SqlTypeName.TinyInt or SqlTypeName.SmallInt or SqlTypeName.Int or SqlTypeName.BigInt => new IntegerSqlValue
                {
                    Value = ReadInteger(source: source, type: column.Type.Name),
                    StorageFamily = SqlStorageFamily.Integer
                },
                SqlTypeName.Decimal or SqlTypeName.Numeric or SqlTypeName.SmallMoney or SqlTypeName.Money => DecodeDecimal(source: source, type: column.Type),
                SqlTypeName.Real => CreateFloatSqlValue(value: BinaryPrimitives.ReadSingleLittleEndian(source: source)),
                SqlTypeName.Float when column.Type.Precision <= 24 => CreateFloatSqlValue(value: BinaryPrimitives.ReadSingleLittleEndian(source: source)),
                SqlTypeName.Float => CreateFloatSqlValue(value: BinaryPrimitives.ReadDoubleLittleEndian(source: source)),
                SqlTypeName.Date => new DateSqlValue
                {
                    Value = DateOnly.FromDayNumber(dayNumber: BinaryPrimitives.ReadInt32LittleEndian(source: source)),
                    StorageFamily = SqlStorageFamily.Date
                },
                SqlTypeName.Time => new TimeSqlValue
                {
                    Value = new TimeOnly(BinaryPrimitives.ReadInt64LittleEndian(source: source)),
                    StorageFamily = SqlStorageFamily.Time
                },
                SqlTypeName.SmallDateTime or SqlTypeName.DateTime or SqlTypeName.DateTime2 => new DateTimeSqlValue
                {
                    Value = new System.DateTime(BinaryPrimitives.ReadInt64LittleEndian(source: source), DateTimeKind.Unspecified),
                    StorageFamily = SqlStorageFamily.DateTime
                },
                SqlTypeName.DateTimeOffset => DecodeDateTimeOffset(source: source),
                SqlTypeName.RowVersion or SqlTypeName.Timestamp => CreateBinarySqlValue(value: source[..8]),
                SqlTypeName.UniqueIdentifier => new UniqueIdentifierSqlValue
                {
                    Value = new Guid(source[..16], bigEndian: true),
                    StorageFamily = SqlStorageFamily.UniqueIdentifier
                },
                _ => throw new StorageFormatException("Unknown fixed-width SQL column type.")};
        }
        catch (StorageFormatException)
        {
            throw;
        }
        catch (Exception exception)when (exception is ArgumentException or OverflowException)
        {
            throw new StorageFormatException($"Column '{column.Name}' contains an invalid {(column.Type).Name.ToString()} value.", exception);
        }
    }

    private SqlValue DecodeDecimal(ReadOnlySpan<byte> source, SqlType type)
    {
        if (type.Name is SqlTypeName.Money or SqlTypeName.SmallMoney)
        {
            var scaled = type.Name == SqlTypeName.Money ? new System.Numerics.BigInteger(BinaryPrimitives.ReadInt64LittleEndian(source: source)) : new System.Numerics.BigInteger(BinaryPrimitives.ReadInt32LittleEndian(source: source));

            return new DecimalSqlValue
            {
                Value = new SqlDecimal
                {
                    Coefficient = scaled,
                    Scale = 4
                },
                StorageFamily = SqlStorageFamily.Decimal
            };
        }

        var negative = source[0] switch
        {
            0 => false,
            1 => true,
            _ => throw new StorageFormatException("Invalid SQL decimal sign byte.")};

        var coefficient = new System.Numerics.BigInteger(source[1..GetDecimalWidth(type: type)], isUnsigned: true, isBigEndian: false);

        return new DecimalSqlValue
        {
            Value = new SqlDecimal
            {
                Coefficient = negative ? -coefficient : coefficient,
                Scale = type.Scale!.Value
            },
            StorageFamily = SqlStorageFamily.Decimal
        };
    }

    private byte[] EncodeFixedForEncryption(SqlValue value, ColumnDefinition column)
    {
        var plaintextColumn = new ColumnDefinition
        {
            Id = column.Id,
            Name = column.Name,
            Type = column.Type,
            IsNullable = column.IsNullable
        };

        var bytes = new byte[GetFixedWidth(column: plaintextColumn)];
        WriteFixedValue(destination: bytes, value: value, column: plaintextColumn);
        return bytes;
    }

    private byte[] EncodeBinary(ReadOnlySpan<byte> value, SqlType type)
    {
        if (type.Name != SqlTypeName.Binary || type.Length is not { } declared || value.Length == declared)
        {
            return value.ToArray();
        }

        var padded = new byte[declared];
        value.CopyTo(destination: padded);
        return padded;
    }

    private void WriteInteger(Span<byte> destination, long value, SqlTypeName type)
    {
        switch (type)
        {
            case SqlTypeName.TinyInt:
                destination[0] = checked((byte)value);
                break;
            case SqlTypeName.SmallInt:
                BinaryPrimitives.WriteInt16LittleEndian(destination: destination, value: checked((short)value));
                break;
            case SqlTypeName.Int:
                BinaryPrimitives.WriteInt32LittleEndian(destination: destination, value: checked((int)value));
                break;
            case SqlTypeName.BigInt:
                BinaryPrimitives.WriteInt64LittleEndian(destination: destination, value: value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    private long ReadInteger(ReadOnlySpan<byte> source, SqlTypeName type) =>
        type switch
    {
        SqlTypeName.TinyInt => source[0],
        SqlTypeName.SmallInt => BinaryPrimitives.ReadInt16LittleEndian(source: source),
        SqlTypeName.Int => BinaryPrimitives.ReadInt32LittleEndian(source: source),
        SqlTypeName.BigInt => BinaryPrimitives.ReadInt64LittleEndian(source: source),
        _ => throw new ArgumentOutOfRangeException(nameof(type))};

    private int GetDecimalWidth(SqlType type) =>
        type.Precision switch
    {
        <= 9 => 5,
        <= 19 => 9,
        <= 28 => 13,
        _ => 17
    };

    private void WriteDecimal(Span<byte> destination, SqlDecimal value, SqlType type)
    {
        if (type.Name is SqlTypeName.Money or SqlTypeName.SmallMoney)
        {
            var scaled = decimals.RoundSqlDecimal(sqlDecimal: value, scale: 4);

            if (type.Name == SqlTypeName.Money)
            {
                BinaryPrimitives.WriteInt64LittleEndian(destination: destination, value: checked((long)scaled));
            }
            else
            {
                BinaryPrimitives.WriteInt32LittleEndian(destination: destination, value: checked((int)scaled));
            }

            return;
        }

        var coefficient = decimals.RoundSqlDecimal(sqlDecimal: value, scale: type.Scale!.Value);
        destination[0] = coefficient.Sign < 0 ? (byte)1 : (byte)0;
        destination[1..GetDecimalWidth(type: type)].Clear();

        if (!System.Numerics.BigInteger.Abs(value: coefficient)
            .TryWriteBytes(destination: destination[1..GetDecimalWidth(type: type)], bytesWritten: out _, isUnsigned: true, isBigEndian: false))
        {
            throw new ArgumentException("Decimal coefficient does not fit the declared precision.");
        }
    }

    private byte[] EncodeVector(VectorSqlValue vector)
    {
        var elementWidth = vector.BaseType == SqlVectorBaseType.Float32 ? 4 : 2;

        var bytes = new byte[checked(ReadVector(vectorSqlValue: vector)
            .Length * elementWidth)];

        if (vector.BaseType == SqlVectorBaseType.Float32)
        {
            for (var index = 0; index < ReadVector(vectorSqlValue: vector)
            .Length; index++)
            {
                BinaryPrimitives.WriteSingleLittleEndian(destination: bytes.AsSpan(start: index * 4), value: ReadVector(vectorSqlValue: vector)[index]);
            }
        }
        else
        {
            for (var index = 0; index < ReadHalfVector(vectorSqlValue: vector)
            .Length; index++)
            {
                BinaryPrimitives.WriteHalfLittleEndian(destination: bytes.AsSpan(start: index * 2), value: ReadHalfVector(vectorSqlValue: vector)[index]);
            }
        }

        return bytes;
    }

    private SqlValue DecodeVector(ReadOnlySpan<byte> source, SqlType type)
    {
        if (type.VectorBaseType == SqlVectorBaseType.Float32)
        {
            if (source.Length != type.VectorDimensions * 4)
            {
                throw new StorageFormatException("Invalid float32 vector length.");
            }

            var values = new float[type.VectorDimensions!.Value];

            for (var index = 0; index < values.Length; index++)
            {
                values[index] = BinaryPrimitives.ReadSingleLittleEndian(source: source[(index * 4)..]);
            }

            return CreateVectorSqlValueModel(values: values);
        }

        if (source.Length != type.VectorDimensions * 2)
        {
            throw new StorageFormatException("Invalid float16 vector length.");
        }

        var halves = new Half[type.VectorDimensions!.Value];

        for (var index = 0; index < halves.Length; index++)
        {
            halves[index] = BinaryPrimitives.ReadHalfLittleEndian(source: source[(index * 2)..]);
        }

        return CreateHalfVectorSqlValueModel(values: halves);
    }

    private SqlValue DecodeDateTimeOffset(ReadOnlySpan<byte> source)
    {
        var utcTicks = BinaryPrimitives.ReadInt64LittleEndian(source: source);
        var offset = TimeSpan.FromMinutes(minutes: BinaryPrimitives.ReadInt16LittleEndian(source: source[8..]));
        var localTicks = checked(utcTicks + offset.Ticks);

        return new DateTimeOffsetSqlValue
        {
            Value = new System.DateTimeOffset(localTicks, offset),
            StorageFamily = SqlStorageFamily.DateTimeOffset
        };
    }

    private static VectorSqlValue CreateVectorSqlValueModel(IEnumerable<float> values)
    {
        ArgumentNullException.ThrowIfNull(argument: values);
        var snapshot = values.ToArray();

        if (snapshot.Any(predicate: value => !float.IsFinite(f: value)))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(values), message: "SQL vector elements must be finite.");
        }

        var content = new char[snapshot.Length * 2];

        for (var i = 0; i < snapshot.Length; i++)
        {
            var bits = BitConverter.SingleToUInt32Bits(value: snapshot[i] == 0f ? 0f : snapshot[i]);
            content[i * 2] = (char)(bits & 65535);
            content[i * 2 + 1] = (char)(bits >> 16);
        }

        return new VectorSqlValue
        {
            BaseType = SqlVectorBaseType.Float32,
            Content = new string (content),
            StorageFamily = SqlStorageFamily.Vector
        };
    }

    private static VectorSqlValue CreateHalfVectorSqlValueModel(IEnumerable<Half> values)
    {
        ArgumentNullException.ThrowIfNull(argument: values);
        var snapshot = values.ToArray();

        if (snapshot.Any(predicate: value => !Half.IsFinite(value: value)))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(values), message: "SQL vector elements must be finite.");
        }

        var content = snapshot.Select(selector: value => (char)BitConverter.HalfToUInt16Bits(value: value == (Half)0 ? (Half)0 : value))
            .ToArray();

        return new VectorSqlValue
        {
            BaseType = SqlVectorBaseType.Float16,
            Content = new string (content),
            StorageFamily = SqlStorageFamily.Vector
        };
    }

    private static float[] ReadVector(VectorSqlValue vectorSqlValue)
    {
        if (vectorSqlValue.BaseType == SqlVectorBaseType.Float16)
        {
            return ReadHalfVector(vectorSqlValue: vectorSqlValue)
            .Select(selector: value => (float)value)
            .ToArray();
        }

        var content = vectorSqlValue.Content;
        var values = new float[content.Length / 2];

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = BitConverter.UInt32BitsToSingle(value: (uint)content[i * 2] | ((uint)content[i * 2 + 1] << 16));
        }

        return values;
    }

    private static Half[] ReadHalfVector(VectorSqlValue vectorSqlValue) =>
        vectorSqlValue.BaseType == SqlVectorBaseType.Float16 ? vectorSqlValue.Content.Select(selector: item => BitConverter.UInt16BitsToHalf(value: item))
            .ToArray() : [];

    private static SqlValue CreateBinarySqlValue(ReadOnlySpan<byte> value) =>
        new BinarySqlValue
    {
        Content = new string (value.ToArray()
            .Select(selector: item => (char)item)
            .ToArray()),
        StorageFamily = SqlStorageFamily.Binary
    };

    private static ReadOnlyMemory<byte> ReadBinarySqlValue(BinarySqlValue binarySqlValue) =>
        binarySqlValue.Content.Select(selector: item => checked((byte)item))
            .ToArray();

    private static SqlValue CreateFloatSqlValue(double value)
    {
        if (!double.IsFinite(d: value))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(value), message: "SQL floating-point values must be finite.");
        }

        return new FloatSqlValue
        {
            Value = value == 0d ? 0d : value,
            StorageFamily = SqlStorageFamily.Float
        };
    }

    internal static RowScalarPayloadOrchestrationService CreateRowScalarPayloadOrchestrationService() =>
        new RowScalarPayloadOrchestrationService(decimals: new SqlDecimalService(), hierarchies: new SqlHierarchyIdService(), encryption: new CatalogColumnEncryptionService());
}