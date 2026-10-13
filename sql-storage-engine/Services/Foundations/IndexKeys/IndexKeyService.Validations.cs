// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Services.Foundations.IndexKeys;
internal sealed partial class IndexKeyService
{
    private static void ValidateIndexKeySource(ReadOnlySpan<byte> bytes) =>
        Validate(inputs: [(bytes.IsEmpty || bytes.Length > ushort.MaxValue, "Index keys must contain between 1 and 65,535 bytes.")]);

    private static void ValidateIndexKey(sql_storage_engine.Models.Indexes.IndexKey indexKey)
    {
        ArgumentNullException.ThrowIfNull(argument: indexKey);
        Validate(inputs: [(string.IsNullOrEmpty(value: indexKey.Content) || indexKey.Content.Length > ushort.MaxValue, "Invalid index key length.")]);

        foreach (var digit in indexKey.Content)
        {
            Validate(inputs: [(digit > byte.MaxValue, "Index key code units must each represent one byte.")]);
        }
    }

    private static void ValidateIndexKeyComparison(sql_storage_engine.Models.Indexes.IndexKey? [] indexKeys)
    {
        ArgumentNullException.ThrowIfNull(argument: indexKeys);
        Validate(inputs: [(indexKeys.Length != 2, "Exactly two keys are required.")]);
        ArgumentNullException.ThrowIfNull(argument: indexKeys[0]);
        ValidateIndexKey(indexKey: indexKeys[0]!);

        if (indexKeys[1] is not null)
        {
            ValidateIndexKey(indexKey: indexKeys[1]!);
        }
    }

    private static void Validate(ReadOnlySpan<(bool Invalid, string Message)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: input.Message);
            }
        }
    }
}