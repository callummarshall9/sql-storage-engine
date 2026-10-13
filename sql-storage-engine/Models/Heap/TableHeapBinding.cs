// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Heap;
public sealed record TableHeapBinding
{
    public PageId RootPageId { get; init; }
    internal object? Service { get; init; }
}