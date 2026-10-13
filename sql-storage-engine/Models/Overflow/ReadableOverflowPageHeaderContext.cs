// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Overflow;
internal ref struct ReadableOverflowPageHeaderContext
{
    public ReadOnlySpan<byte> page;
    public ulong expectedPageId;
}