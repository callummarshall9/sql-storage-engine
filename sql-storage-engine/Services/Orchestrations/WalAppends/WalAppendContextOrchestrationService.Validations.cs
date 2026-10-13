// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading.Tasks;
using System.Threading;
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.Services.Orchestrations.WalAppends;
internal sealed partial class WalAppendContextOrchestrationService
{
    private void ValidateOpen(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (state.IsOpen)
        {
            throw new InvalidOperationException(message: "WAL is already open.");
        }
    }

    private void ValidateState()
    {
        if (!state.IsOpen)
        {
            throw new InvalidOperationException(message: "WAL has not been opened.");
        }
    }

    private void ValidateAppend(WalAppendContext walAppendContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: walAppendContext);
        ArgumentNullException.ThrowIfNull(argument: walAppendContext.Record);
        ValidateState();
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void ValidateFlush(ulong logSequenceNumber, CancellationToken cancellationToken)
    {
        ValidateState();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfZero(value: logSequenceNumber);
    }

    private void ValidateGuard(ulong logSequenceNumber, CancellationToken cancellationToken)
    {
        ValidateState();
    }

    private void ValidateTransactionId(ulong transactionId)
    {
        ValidateState();
        ArgumentOutOfRangeException.ThrowIfZero(value: transactionId);
    }

    private void ValidateTransaction(WalAppendContext walAppendContext)
    {
        ArgumentNullException.ThrowIfNull(argument: walAppendContext);
        ArgumentOutOfRangeException.ThrowIfZero(value: walAppendContext.TransactionId.Value);
        ValidateState();
    }

    private void ValidateActiveTransaction(WalAppendContext walAppendContext)
    {
        ValidateTransaction(walAppendContext: walAppendContext);

        if (walAppendContext.TransactionState != sql_storage_engine.Transactions.TransactionState.Active)
        {
            throw new InvalidOperationException(message: $"Durable transaction {walAppendContext.TransactionId} is {walAppendContext.TransactionState}.");
        }
    }

    private void ValidateChange(WalAppendContext walAppendContext, ReadOnlyMemory<byte> physicalChange, CancellationToken cancellationToken)
    {
        ValidateActiveTransaction(walAppendContext: walAppendContext);
    }

    private void ValidateCommit(WalAppendContext walAppendContext, Func<ValueTask>? beforeResponse, CancellationToken cancellationToken)
    {
        ValidateActiveTransaction(walAppendContext: walAppendContext);
    }

    private void ValidateCheckpoint(WalAppendContext walAppendContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: walAppendContext);
        ArgumentNullException.ThrowIfNull(argument: walAppendContext.Checkpoint);
        ArgumentNullException.ThrowIfNull(argument: walAppendContext.CheckpointProvider);
        ArgumentNullException.ThrowIfNull(argument: walAppendContext.PublishCheckpoint);
        ValidateState();
        cancellationToken.ThrowIfCancellationRequested();
    }
}