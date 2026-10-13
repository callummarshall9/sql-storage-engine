// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.MemoryPages;
internal sealed class MemoryPageState
{
    public object Sync { get => sync!; init => sync = value; }
    public Dictionary<PageId, byte[]> Pages { get => pages!; init => pages = value; }
    public Stack<PageId> Free { get => free!; init => free = value; }

    private object? sync;
    private Dictionary<PageId, byte[]>? pages;
    private Stack<PageId>? free;
    public int PageSize { get; init; }

    public ulong NextPageId;
    public bool IsClosed;
}