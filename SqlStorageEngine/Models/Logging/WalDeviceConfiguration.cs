// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Models.Logging;
internal sealed record WalDeviceConfiguration
{
    public object? ProviderSource { get; set; }
    public Func<long>? Length { get; set; }
    public Func<long, Memory<byte>, CancellationToken, ValueTask<int>>? Read { get; set; }
    public Func<long, ReadOnlyMemory<byte>, CancellationToken, ValueTask<int>>? Write { get; set; }
    public Func<CancellationToken, ValueTask>? Flush { get; set; }
    public Func<ulong, CancellationToken, ValueTask>? Roll { get; set; }
    public Action<long>? Truncate { get; set; }
    public int MaximumSegmentBytes { get; set; }
}