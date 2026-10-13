// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Indexes;
internal ref struct ReadableLeafIndexEntryContext
{
    public LeafIndexEntry leafIndexEntry;
    public ReadOnlySpan<byte> includedPayload;
}