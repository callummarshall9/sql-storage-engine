// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;
using sql_storage_engine.Catalog;

namespace sql_storage_engine.Services.Aggregations.SqlValues;
internal sealed partial class SqlValueRequestAggregationService
{
    private static void ValidateCreation(SqlValueRequest sqlValueRequest)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlValueRequest);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: sqlValueRequest.Operation);
    }

    private static void ValidateComparison(SqlValueRequest sqlValueRequest)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlValueRequest);
        ArgumentNullException.ThrowIfNull(argument: sqlValueRequest.SqlValues);

        if (sqlValueRequest.SqlValues.Length != 2)
        {
            throw new ArgumentException(message: "SQL comparison requires two values.");
        }

        foreach (var value in sqlValueRequest.SqlValues)
        {
            ArgumentNullException.ThrowIfNull(argument: value);
        }
    }

    private static void ValidateBinaryRead(SqlValueRequest sqlValueRequest)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlValueRequest);

        if (sqlValueRequest.Input is not BinarySqlValue binary)
        {
            throw new ArgumentException(message: "A binary SQL value is required.");
        }

        ArgumentNullException.ThrowIfNull(argument: binary.Content);

        if (binary.StorageFamily != SqlStorageFamily.Binary || binary.IsNull || binary.Content.Any(predicate: item => item > 255))
        {
            throw new ArgumentException(message: "Binary SQL value metadata or content is invalid.");
        }
    }

    private static void ValidateVector(VectorSqlValue vectorSqlValue)
    {
        ArgumentNullException.ThrowIfNull(argument: vectorSqlValue);
        ArgumentNullException.ThrowIfNull(argument: vectorSqlValue.Content);

        if (vectorSqlValue.StorageFamily != SqlStorageFamily.Vector || vectorSqlValue.IsNull || !Enum.IsDefined(value: vectorSqlValue.BaseType) || vectorSqlValue.BaseType == SqlVectorBaseType.Float32 && vectorSqlValue.Content.Length % 2 != 0)
        {
            throw new ArgumentException(message: "Vector SQL value metadata or content is invalid.");
        }

        if (ReadVector(vectorSqlValue: vectorSqlValue)
            .Any(predicate: value => !float.IsFinite(f: value)))
        {
            throw new ArgumentException(message: "SQL vector elements must be finite.");
        }
    }

    private static void ValidateVectorRead(SqlValueRequest sqlValueRequest)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlValueRequest);

        if (sqlValueRequest.Input is not VectorSqlValue vector)
        {
            throw new ArgumentException(message: "A vector SQL value is required.");
        }

        ValidateVector(vectorSqlValue: vector);
    }

    private static void ValidateVectorDistance(SqlValueRequest sqlValueRequest)
    {
        ValidateComparison(sqlValueRequest: sqlValueRequest);

        foreach (var value in sqlValueRequest.SqlValues!)
        {
            if (value is not VectorSqlValue vector)
            {
                throw new ArgumentException(message: "Vector distance requires vector SQL values.");
            }

            ValidateVector(vectorSqlValue: vector);
        }

        if (sqlValueRequest.Other is not SqlVectorDistanceMetric metric || !Enum.IsDefined(value: metric))
        {
            throw new ArgumentOutOfRangeException(paramName: "metric");
        }
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
}