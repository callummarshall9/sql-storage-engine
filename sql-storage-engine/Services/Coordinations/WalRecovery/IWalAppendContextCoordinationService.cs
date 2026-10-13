// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.Services.Coordinations.WalRecovery;
internal interface IWalAppendContextCoordinationService
{
    ValueTask<WalAppendContext> AppendWalAppendContextAsync(WalAppendContext walAppendContext, CancellationToken cancellationToken);

    ValueTask FlushWalAppendContextAsync(ulong logSequenceNumber, CancellationToken cancellationToken);

    WalAppendContext ReadWalAppendContext();

    ValueTask GuardWalAppendContextAsync(ulong logSequenceNumber, CancellationToken cancellationToken);

    WalAppendContext CreateWalAppendContextTransaction(ulong transactionId);

    WalAppendContext ReadWalAppendContextTransaction(WalAppendContext walAppendContext);

    void CheckWalAppendContextTransaction(WalAppendContext walAppendContext);

    ValueTask<ulong> AppendWalAppendContextChangeAsync(WalAppendContext walAppendContext, ReadOnlyMemory<byte> physicalChange, CancellationToken cancellationToken);

    ValueTask CommitWalAppendContextTransactionAsync(WalAppendContext walAppendContext, Func<ValueTask>? beforeResponse, CancellationToken cancellationToken);

    ValueTask<ulong> AppendWalAppendContextCheckpointAsync(WalAppendContext walAppendContext, CancellationToken cancellationToken);

    ValueTask RedoWalAppendContextAsync(WalAppendContext walAppendContext, CancellationToken cancellationToken);

    ValueTask<bool> UndoWalAppendContextAsync(WalAppendContext walAppendContext, CancellationToken cancellationToken);
}