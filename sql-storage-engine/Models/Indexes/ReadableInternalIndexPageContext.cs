// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Indexes;
internal ref struct ReadableInternalIndexPageContext
{
    public ReadOnlySpan<byte> page;
    public ulong expectedPageId;
}