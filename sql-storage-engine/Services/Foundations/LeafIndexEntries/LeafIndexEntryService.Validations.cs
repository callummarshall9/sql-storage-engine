// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.Services.Foundations.LeafIndexEntries;
internal sealed partial class LeafIndexEntryService
{
    private static void ValidateLeafIndexEntryOnCreate(LeafIndexEntry leafIndexEntry, ReadOnlySpan<byte> includedPayload) =>
        Validate(inputs: [(leafIndexEntry.Key is null, "Leaf keys cannot be null."), (includedPayload.Length > ushort.MaxValue, "Leaf included payload exceeds 65,535 bytes.")]);

    private static void ValidateLeafIndexEntryPayload(LeafIndexEntry leafIndexEntry)
    {
        Validate(inputs: [(leafIndexEntry.Key is null, "Leaf keys cannot be null.")]);
        ArgumentNullException.ThrowIfNull(argument: leafIndexEntry.PayloadContent);
        Validate(inputs: [(leafIndexEntry.PayloadContent.Length > ushort.MaxValue, "Invalid leaf payload length.")]);

        foreach (var digit in leafIndexEntry.PayloadContent)
        {
            Validate(inputs: [(digit > byte.MaxValue, "Leaf payload code units must each represent one byte.")]);
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