// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.Services.Coordinations.WalRecovery;
internal sealed partial class WalAppendContextCoordinationService
{
    private void ValidateWal()
    {
        if (wal is null)
        {
            throw new InvalidOperationException(message: "WAL is not attached to this recovery service.");
        }
    }

    private void ValidateContext(WalAppendContext walAppendContext)
    {
        ValidateWal();
        ArgumentNullException.ThrowIfNull(argument: walAppendContext);
    }

    private static void ValidateRecovery(WalAppendContext walAppendContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: walAppendContext);
        ArgumentNullException.ThrowIfNull(argument: walAppendContext.Recovery);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void ValidateUndo(WalAppendContext walAppendContext, CancellationToken cancellationToken)
    {
        ValidateRecovery(walAppendContext: walAppendContext, cancellationToken: cancellationToken);
        ValidateWal();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value: walAppendContext.Recovery!.MaximumActions);
    }

    private void ValidateWalAppendContextAsync(WalAppendContext walAppendContext, CancellationToken cancellationToken)
    {
        ValidateContext(walAppendContext: walAppendContext);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void ValidateFlushWalAppendContextAsync(ulong logSequenceNumber, CancellationToken cancellationToken)
    {
        ValidateWal();
        ArgumentOutOfRangeException.ThrowIfZero(value: logSequenceNumber);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void ValidateReadWalAppendContext()
    {
        ValidateWal();
    }

    private void ValidateGuardWalAppendContextAsync(ulong logSequenceNumber, CancellationToken cancellationToken)
    {
        ValidateWal();
    }

    private void ValidateCreateWalAppendContextTransaction(ulong transactionId)
    {
        ValidateWal();
        ArgumentOutOfRangeException.ThrowIfZero(value: transactionId);
    }

    private void ValidateWalTransactionContext(WalAppendContext walAppendContext)
    {
        ValidateContext(walAppendContext: walAppendContext);
    }

    private void ValidateAppendWalAppendContextChangeAsync(WalAppendContext walAppendContext, ReadOnlyMemory<byte> physicalChange, CancellationToken cancellationToken)
    {
        ValidateContext(walAppendContext: walAppendContext);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void ValidateCommitWalAppendContextTransactionAsync(WalAppendContext walAppendContext, Func<ValueTask>? beforeResponse, CancellationToken cancellationToken)
    {
        ValidateContext(walAppendContext: walAppendContext);
    }
}