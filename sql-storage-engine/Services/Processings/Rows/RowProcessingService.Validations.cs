// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Services.Processings.Rows;
internal sealed partial class RowProcessingService
{
    private static void ValidateRow(Row row) =>
        Validate(inputs: [(row is null, true, nameof(row)), (row?.ValueSource is null && row?.Values is null, true, "values")]);

    private static void ValidateValues(SqlValue[] values)
    {
        Validate(inputs: [(values.Any(predicate: value => value is null), false, nameof(values))]);

        foreach (var value in values)
        {
            SqlStorageFamily? expected = value switch
            {
                NullSqlValue or DefaultSqlValue or CursorSqlValue or TableSqlValue => null,
                BooleanSqlValue => SqlStorageFamily.Boolean,
                IntegerSqlValue => SqlStorageFamily.Integer,
                TextSqlValue => SqlStorageFamily.Text,
                BinarySqlValue or HierarchyIdSqlValue or SpatialSqlValue => SqlStorageFamily.Binary,
                DecimalSqlValue => SqlStorageFamily.Decimal,
                FloatSqlValue => SqlStorageFamily.Float,
                DateSqlValue => SqlStorageFamily.Date,
                TimeSqlValue => SqlStorageFamily.Time,
                DateTimeSqlValue => SqlStorageFamily.DateTime,
                DateTimeOffsetSqlValue => SqlStorageFamily.DateTimeOffset,
                UniqueIdentifierSqlValue => SqlStorageFamily.UniqueIdentifier,
                VariantSqlValue => SqlStorageFamily.Variant,
                VectorSqlValue => SqlStorageFamily.Vector,
                _ => throw new ArgumentException(message: "Unknown SQL value representation.", paramName: nameof(values))};

            if (value.StorageFamily != expected || value.IsNull != (value is NullSqlValue))
            {
                throw new ArgumentException(message: "SQL value metadata contradicts its representation.", paramName: nameof(values));
            }

            if (value is BinarySqlValue binary && (binary.Content is null || binary.Content.Any(predicate: item => item > 255)))
            {
                throw new ArgumentException(message: "Binary SQL value content is invalid.", paramName: nameof(values));
            }

            if (value is VectorSqlValue vector)
            {
                ValidateVectorContent(vectorSqlValue: vector);
            }

            if (value is FloatSqlValue approximate && !double.IsFinite(d: approximate.Value))
            {
                throw new ArgumentException(message: "SQL floating-point values must be finite.", paramName: nameof(values));
            }

            if (value is TextSqlValue text && text.Value is null)
            {
                throw new ArgumentException(message: "SQL text cannot contain a null CLR reference.", paramName: nameof(values));
            }
        }
    }

    private static void ValidateVectorContent(VectorSqlValue vectorSqlValue)
    {
        if (vectorSqlValue.Content is null || !Enum.IsDefined(value: vectorSqlValue.BaseType) || vectorSqlValue.BaseType == SqlVectorBaseType.Float32 && vectorSqlValue.Content.Length % 2 != 0)
        {
            throw new ArgumentException(message: "Vector SQL value content is invalid.");
        }

        var stride = vectorSqlValue.BaseType == SqlVectorBaseType.Float32 ? 2 : 1;

        for (var index = 0; index < vectorSqlValue.Content.Length; index += stride)
        {
            var component = stride == 1 ? (float)BitConverter.UInt16BitsToHalf(value: vectorSqlValue.Content[index]) : BitConverter.UInt32BitsToSingle(value: (uint)vectorSqlValue.Content[index] | ((uint)vectorSqlValue.Content[index + 1] << 16));

            if (!float.IsFinite(f: component))
            {
                throw new ArgumentException(message: "SQL vector elements must be finite.");
            }
        }
    }

    private static void Validate((bool Invalid, bool Missing, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (!input.Invalid)
            {
                continue;
            }

            if (input.Missing)
            {
                throw new ArgumentNullException(paramName: input.ParameterName);
            }

            throw new ArgumentException(message: "Rows cannot contain null CLR references.", paramName: input.ParameterName);
        }
    }
}