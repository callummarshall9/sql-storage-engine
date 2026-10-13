// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Brokers.RecoveryPages;
internal interface IRecoveryPageBroker
{
    ValueTask ReadAsync(Func<ulong, Memory<byte>, CancellationToken, ValueTask> read, ulong pageId, Memory<byte> destination, CancellationToken cancellationToken);

    ValueTask WriteAsync(Func<ulong, ReadOnlyMemory<byte>, CancellationToken, ValueTask> write, ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken);

    int ReadPageSize(Func<int> pageSize);
}