// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Indexes;
internal ref struct ReadableLeafIndexPageContext
{
    public ReadOnlySpan<byte> page;
    public ulong expectedPageId;
}