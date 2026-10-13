// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;

namespace sql_storage_engine.Models.Buffers;
internal sealed class BufferPoolRuntime
{
    public Dictionary<ulong, BufferFrameState>? Frames;
    public List<BufferFrameState>? Clock;
    public int ClockHand;
    public long HitCount;
    public long MissCount;
    public bool Disposed;
}