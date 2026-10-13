// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Heap;
internal sealed record HeapPageLease
{
    public Guid Id { get; init; }
    public Memory<byte> Memory { get; init; }
}