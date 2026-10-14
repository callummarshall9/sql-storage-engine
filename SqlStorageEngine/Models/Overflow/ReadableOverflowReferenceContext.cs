// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Overflow;
internal ref struct ReadableOverflowReferenceContext
{
    public ReadOnlySpan<byte> source;
}