// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Indexes;
internal ref struct ReadableIndexKeyContext
{
    public ReadOnlySpan<byte> bytes;
}