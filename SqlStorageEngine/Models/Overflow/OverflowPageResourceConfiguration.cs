// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Models.Overflow;
internal sealed record OverflowPageResourceConfiguration
{
    public object? BufferSource { get; set; }
    public object? AllocatorSource { get; set; }
    public Func<int>? PageSize { get; set; }
    public Func<ulong, CancellationToken, ValueTask<(IDisposable Lease, Memory<byte> Memory, Action<ulong> MarkDirty)>>? Pin { get; set; }
    public Func<CancellationToken, ValueTask<ulong>>? Allocate { get; set; }
    public Func<ulong, CancellationToken, ValueTask>? Discard { get; set; }
    public Func<ulong, CancellationToken, ValueTask>? Free { get; set; }
}