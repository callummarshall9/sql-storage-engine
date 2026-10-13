// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System;

namespace sql_storage_engine.Models.Heap;
public readonly record struct HeapPageRow
{
    public SlotId SlotId { get; init; }
    public SlotGeneration Generation { get; init; }
    public ReadOnlyMemory<byte> Bytes { get; init; }
}