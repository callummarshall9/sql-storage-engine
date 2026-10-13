// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.Services.Foundations.LeafIndexEntries;
internal interface ILeafIndexEntryService
{
    LeafIndexEntry CreateLeafIndexEntry(LeafIndexEntry leafIndexEntry, ReadOnlySpan<byte> includedPayload);

    ReadOnlyMemory<byte> ReadLeafIndexEntryPayload(LeafIndexEntry leafIndexEntry);
}