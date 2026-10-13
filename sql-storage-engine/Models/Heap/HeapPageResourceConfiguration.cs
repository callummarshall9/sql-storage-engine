// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Models.Heap;
internal sealed record HeapPageResourceConfiguration
{
    public object? BufferSource { get; set; }
    public object? AllocatorSource { get; set; }
    public Func<int>? PageSize { get; set; }
    public Func<ulong, CancellationToken, ValueTask<(IDisposable Lease, Memory<byte> Memory, Action<ulong> MarkDirty)>>? Pin { get; set; }
    public Func<CancellationToken, ValueTask<ulong>>? Allocate { get; set; }
    public Func<ulong, CancellationToken, ValueTask>? Discard { get; set; }
    public Func<ulong, CancellationToken, ValueTask>? Free { get; set; }
    public object? FreeSpaceSource { get; set; }
    public Func<int, ulong?>? Find { get; set; }
    public Action<ulong, int>? Update { get; set; }
    public Action<ulong>? Remove { get; set; }
    public Action? Clear { get; set; }
    public ulong RootPageId { get; set; }
    public bool FreeSpaceRequired { get; set; }
}