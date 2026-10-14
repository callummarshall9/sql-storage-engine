// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Overflow;
internal ref struct WritableOverflowPageHeaderContext
{
    public Span<byte> page;
    public ulong pageId;
    public ulong? nextPageId;
    public ReadOnlySpan<byte> payload;
}