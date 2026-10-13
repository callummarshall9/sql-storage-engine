// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.Services.Foundations.LeafIndexEntries;
internal sealed partial class LeafIndexEntryService : ILeafIndexEntryService
{
    public LeafIndexEntry CreateLeafIndexEntry(LeafIndexEntry leafIndexEntry, ReadOnlySpan<byte> includedPayload) =>
        TryCatch(operation: static context =>
    {
        ValidateLeafIndexEntryOnCreate(leafIndexEntry: context.leafIndexEntry, includedPayload: context.includedPayload);

        return context.leafIndexEntry with
        {
            PayloadContent = string.Create(length: context.includedPayload.Length, state: context.includedPayload, action: static (destination, source) =>
            {
                for (var index = 0; index < source.Length; index++)
                {
                    destination[index] = (char)source[index];
                }
            })
        };
    }, context: new ReadableLeafIndexEntryContext { leafIndexEntry = leafIndexEntry, includedPayload = includedPayload });

    public ReadOnlyMemory<byte> ReadLeafIndexEntryPayload(LeafIndexEntry leafIndexEntry) =>
        TryCatch(operation: () =>
    {
        ValidateLeafIndexEntryPayload(leafIndexEntry: leafIndexEntry);
        return new ReadOnlyMemory<byte>(ReadLeafIndexEntryPayloadCore(leafIndexEntry: leafIndexEntry));
    });

    private static byte[] ReadLeafIndexEntryPayloadCore(LeafIndexEntry leafIndexEntry)
    {
        var bytes = new byte[leafIndexEntry.PayloadContent.Length];

        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] = (byte)leafIndexEntry.PayloadContent[index];
        }

        return bytes;
    }
}