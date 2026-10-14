// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Buffers;
internal sealed record BufferPoolContext
{
    public BufferPoolConfiguration? Configuration { get; init; }
    public BufferPoolRuntime? Runtime { get; init; }
    public BufferFrameState? ResultFrame { get; init; }
    public ulong? ResultPinId { get; init; }
    public int PageSize { get; init; }
    public int FrameCount { get; init; }
    public int PinnedPageCount { get; init; }
    public long HitCount { get; init; }
    public long MissCount { get; init; }
    public System.IDisposable? Lifetime { get; init; }
    public System.Action? PinRelease { get; init; }
    public System.Action<ulong>? PinDirty { get; init; }
}