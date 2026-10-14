// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Models.Buffers;
internal sealed record BufferPoolConfiguration
{
    public object? ProviderSource { get; set; }
    public Func<int>? PageSize { get; set; }
    public Func<ulong, Memory<byte>, CancellationToken, ValueTask>? Read { get; set; }
    public Func<ulong, ReadOnlyMemory<byte>, CancellationToken, ValueTask>? Write { get; set; }
    public Func<CancellationToken, ValueTask>? Flush { get; set; }
    public Func<ValueTask>? Close { get; set; }
    public Func<ulong, ulong, CancellationToken, ValueTask>? Guard { get; set; }
    public bool GuardPresent { get; set; }
    public bool LeaveOpen { get; set; }
    public int Capacity { get; set; }
}