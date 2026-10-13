// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Foundations.RandomSequences;
internal sealed partial class RandomSequenceService
{
    private static void ValidateRandomSequence(RandomSequence randomSequence)
    {
        ArgumentNullException.ThrowIfNull(argument: randomSequence);
        Validate(inputs: [(randomSequence.Maximum <= 0, nameof(randomSequence.Maximum)), (randomSequence.Count < 0, nameof(randomSequence.Count))]);
    }

    private static void Validate(ReadOnlySpan<(bool Invalid, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentOutOfRangeException(paramName: input.ParameterName);
            }
        }
    }
}