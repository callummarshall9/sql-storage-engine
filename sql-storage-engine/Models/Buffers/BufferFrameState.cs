// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;

namespace sql_storage_engine.Models.Buffers;
internal sealed class BufferFrameState
{
    public object? Sync;
    public ulong PageId;
    public byte[]? Bytes;
    public HashSet<ulong>? Pins;
    public ulong NextPin;
    public bool IsDirty;
    public ulong PageLogSequenceNumber;
    public bool RecentlyUsed;
    public ulong DirtyGeneration;
}