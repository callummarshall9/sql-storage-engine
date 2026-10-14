// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Services.Foundations.LeafIndexEntries;

namespace sql_storage_engine.Exposures.Indexes;
public static class LeafIndexEntryExposure
{
    private static readonly ILeafIndexEntryService service = new LeafIndexEntryService();
    public static LeafIndexEntry CreateLeafIndexEntry(IndexKey indexKey, RowId rowId, ReadOnlyMemory<byte> includedPayload = default) =>
        service.CreateLeafIndexEntry(leafIndexEntry: new LeafIndexEntry { Key = indexKey, RowId = rowId }, includedPayload: includedPayload.Span);

    public static ReadOnlyMemory<byte> ReadLeafIndexEntryPayload(LeafIndexEntry leafIndexEntry) =>
        service.ReadLeafIndexEntryPayload(leafIndexEntry: leafIndexEntry);
}