// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Models.Logging;
internal sealed record RecoveryPageContext
{
    public object? ProviderSource { get; init; }
    public Func<int>? PageSize { get; init; }
    public Func<ulong, Memory<byte>, CancellationToken, ValueTask>? Read { get; init; }
    public Func<ulong, ReadOnlyMemory<byte>, CancellationToken, ValueTask>? Write { get; init; }
    public RecoveryAnalysis? Analysis { get; init; }
    public ulong PageId { get; init; }
    public byte[]? Image { get; init; }
    public PhysicalPageChange? Change { get; init; }
    public byte[]? Payload { get; init; }
    public bool CheckExpectedLsn { get; init; }
    public ulong ExpectedLsn { get; init; }
    public ulong StoredLsn { get; init; }
    public int MaximumActions { get; init; }
    public bool Complete { get; init; }
}