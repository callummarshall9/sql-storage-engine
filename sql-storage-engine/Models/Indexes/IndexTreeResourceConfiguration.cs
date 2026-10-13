// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Models.Indexes;
internal sealed record IndexTreeResourceConfiguration
{
    public IndexAllocationContext? Allocations { get; set; }
    public object? BufferSource { get; set; }
    public object? AllocatorSource { get; set; }
    public IndexRootContext? RootContext { get; set; }
    public object? RootSource { get; set; }
    public Func<int>? PageSize { get; set; }
    public Func<ulong, CancellationToken, ValueTask<(IDisposable Lease, Memory<byte> Memory, Action<ulong> MarkDirty)>>? Pin { get; set; }
    public Func<ushort, CancellationToken, ValueTask<ulong>>? Allocate { get; set; }
    public Func<ulong>? ReadRoot { get; set; }
    public Func<ulong, CancellationToken, ValueTask>? UpdateRoot { get; set; }
    public bool IsUnique { get; set; }
}