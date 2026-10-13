// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;

namespace sql_storage_engine.Models.Heap;
internal sealed class FreeSpaceState
{
    public object Sync { get => sync!; init => sync = value; }
    public Dictionary<ulong, FreeSpaceEntry> Entries { get => entries!; init => entries = value; }

    private object? sync;
    private Dictionary<ulong, FreeSpaceEntry>? entries;
    public int PageSize { get; init; }
}