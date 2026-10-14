// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Overflow;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.OverflowReferences;
internal sealed partial class OverflowReferenceService
{
    private static void ValidateOverflowReferenceOnWrite(Span<byte> destination, OverflowReference overflowReference)
    {
        ValidateArguments(inputs: [(destination.Length < OverflowReferenceFormat.EncodedLength, nameof(destination))]);
        ValidateOverflowReference(overflowReference: overflowReference);
    }

    private static void ValidateOverflowReferenceSource(ReadOnlySpan<byte> source) =>
        Validate(inputs: [(source.Length < OverflowReferenceFormat.EncodedLength, "Overflow reference is truncated.")]);

    private static void ValidateOverflowReference(OverflowReference overflowReference) =>
        Validate(inputs: [(overflowReference.FirstPageId.Value == 0, "Overflow chains cannot begin at page zero."), (overflowReference.TotalLength <= 0 || overflowReference.TotalLength > OverflowReferenceFormat.MaximumValueLength, $"Overflow length must be between 1 and {OverflowReferenceFormat.MaximumValueLength} bytes.")]);

    private static void ValidateArguments(ReadOnlySpan<(bool Invalid, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: "Overflow reference destination is truncated.", paramName: input.ParameterName);
            }
        }
    }

    private static void Validate(ReadOnlySpan<(bool Invalid, string Message)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new StorageFormatException(message: input.Message);
            }
        }
    }
}