// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Diagnostics;

namespace sql_storage_engine.Services.Processings.PartialWrites;
internal sealed partial class RandomSequencePartialWriteProcessingService
{
    private static void ValidatePartialWrite(ReadOnlySpan<byte> original, ReadOnlySpan<byte> replacement, PartialWritePattern pattern, int seed, int sectorSize) =>
        Validate(inputs: [(original.IsEmpty || original.Length != replacement.Length, false, nameof(original)), (!Enum.IsDefined(value: pattern), true, nameof(pattern)), (sectorSize <= 0 || sectorSize > original.Length, true, nameof(sectorSize))]);

    private static void Validate(ReadOnlySpan<(bool Invalid, bool Range, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (!input.Invalid)
            {
                continue;
            }

            if (input.Range)
            {
                throw new ArgumentOutOfRangeException(paramName: input.ParameterName);
            }

            throw new ArgumentException(message: "Write images must have one non-empty length.", paramName: input.ParameterName);
        }
    }
}