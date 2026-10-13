// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Overflow;
internal ref struct WritableOverflowReferenceContext
{
    public Span<byte> destination;
    public OverflowReference overflowReference;
}