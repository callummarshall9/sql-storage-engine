// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Overflow;
internal sealed record OverflowChainContext
{
    public OverflowReference? Reference { get; init; }
    public ReadOnlyMemory<byte> Value { get; init; }
}