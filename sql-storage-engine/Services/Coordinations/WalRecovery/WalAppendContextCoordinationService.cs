// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Logging;
using System.Linq;
using sql_storage_engine.Logging;
using sql_storage_engine.Transactions;
using sql_storage_engine.Storage;
using sql_storage_engine.Services.Orchestrations.WalAppends;
using sql_storage_engine.Services.Orchestrations.RecoveryPages;

namespace sql_storage_engine.Services.Coordinations.WalRecovery;
internal sealed partial class WalAppendContextCoordinationService : IWalAppendContextCoordinationService
{
    private readonly WalAppendContextOrchestrationService? wal;
    private readonly RecoveryPageContextOrchestrationService recovery;
    private WalAppendContextCoordinationService(WalAppendContextOrchestrationService? wal, RecoveryPageContextOrchestrationService recovery)
    {
        this.wal = wal;
        this.recovery = recovery;
    }

    internal static WalAppendContextCoordinationService CreateWalAppendContextCoordinationService(WalDeviceConfiguration walDeviceConfiguration) =>
        new WalAppendContextCoordinationService(wal: WalAppendContextOrchestrationService.CreateWalAppendContextOrchestrationService(walDeviceConfiguration: walDeviceConfiguration), recovery: RecoveryPageContextOrchestrationService.CreateRecoveryPageContextOrchestrationService());

    internal static WalAppendContextCoordinationService CreateWalAppendContextCoordinationServiceReplay() =>
        new WalAppendContextCoordinationService(wal: null, recovery: RecoveryPageContextOrchestrationService.CreateRecoveryPageContextOrchestrationService());

    internal async ValueTask<WalAppendContextCoordinationService> OpenWalAppendContextCoordinationServiceAsync(CancellationToken cancellationToken)
    {
        await wal!.OpenWalAppendContextOrchestrationServiceAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return this;
    }

    public ValueTask<WalAppendContext> AppendWalAppendContextAsync(WalAppendContext walAppendContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateWalAppendContextAsync(walAppendContext: walAppendContext, cancellationToken: cancellationToken);

        return await wal!.AppendWalAppendContextAsync(walAppendContext: walAppendContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask FlushWalAppendContextAsync(ulong logSequenceNumber, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateFlushWalAppendContextAsync(logSequenceNumber: logSequenceNumber, cancellationToken: cancellationToken);

        await wal!.FlushWalAppendContextAsync(logSequenceNumber: logSequenceNumber, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public WalAppendContext ReadWalAppendContext() =>
        TryCatch(operation: () =>
    {
        ValidateReadWalAppendContext();
        return wal!.ReadWalAppendContext();
    });

    public ValueTask GuardWalAppendContextAsync(ulong logSequenceNumber, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateGuardWalAppendContextAsync(logSequenceNumber: logSequenceNumber, cancellationToken: cancellationToken);

        await wal!.GuardWalAppendContextAsync(logSequenceNumber: logSequenceNumber, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public WalAppendContext CreateWalAppendContextTransaction(ulong transactionId) =>
        TryCatch(operation: () =>
    {
        ValidateCreateWalAppendContextTransaction(transactionId: transactionId);
        return wal!.CreateWalAppendContextTransaction(transactionId: transactionId);
    });

    public WalAppendContext ReadWalAppendContextTransaction(WalAppendContext walAppendContext) =>
        TryCatch(operation: () =>
    {
        ValidateWalTransactionContext(walAppendContext: walAppendContext);
        return wal!.ReadWalAppendContextTransaction(walAppendContext: walAppendContext);
    });

    public void CheckWalAppendContextTransaction(WalAppendContext walAppendContext) =>
        TryCatch(operation: () =>
    {
        ValidateWalTransactionContext(walAppendContext: walAppendContext);
        wal!.CheckWalAppendContextTransaction(walAppendContext: walAppendContext);
    });

    public ValueTask<ulong> AppendWalAppendContextChangeAsync(WalAppendContext walAppendContext, ReadOnlyMemory<byte> physicalChange, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateAppendWalAppendContextChangeAsync(walAppendContext: walAppendContext, physicalChange: physicalChange, cancellationToken: cancellationToken);

        return await wal!.AppendWalAppendContextChangeAsync(walAppendContext: walAppendContext, physicalChange: physicalChange, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask CommitWalAppendContextTransactionAsync(WalAppendContext walAppendContext, Func<ValueTask>? beforeResponse, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCommitWalAppendContextTransactionAsync(walAppendContext: walAppendContext, beforeResponse: beforeResponse, cancellationToken: cancellationToken);

        await wal!.CommitWalAppendContextTransactionAsync(walAppendContext: walAppendContext, beforeResponse: beforeResponse, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<ulong> AppendWalAppendContextCheckpointAsync(WalAppendContext walAppendContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateWalAppendContextAsync(walAppendContext: walAppendContext, cancellationToken: cancellationToken);

        return await wal!.AppendWalAppendContextCheckpointAsync(walAppendContext: walAppendContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask RedoWalAppendContextAsync(WalAppendContext walAppendContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRecovery(walAppendContext: walAppendContext, cancellationToken: cancellationToken);

        await recovery.RedoRecoveryPageContextAsync(recoveryPageContext: walAppendContext.Recovery!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<bool> UndoWalAppendContextAsync(WalAppendContext walAppendContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateUndo(walAppendContext: walAppendContext, cancellationToken: cancellationToken);
        var source = recovery.ReadRecoveryPageContextAnalysis(recoveryPageContext: walAppendContext.Recovery!);
        var analysis = source.Analysis!;
        var records = analysis.Records.ToDictionary(keySelector: record => record.Lsn);
        var actions = 0;

        foreach (var transaction in analysis.Transactions.Where(predicate: pair => pair.Value == TransactionState.Active))
        {
            var current = analysis.Records.Where(predicate: record => record.TransactionId == transaction.Key)
            .OrderByDescending(keySelector: record => record.Lsn.Value)
            .FirstOrDefault();

            var previous = current?.Lsn ?? default;

            while (previous.Value != 0)
            {
                if (!records.TryGetValue(key: previous, value: out var record))
                {
                    throw new StorageCorruptionException("Transaction previous-LSN chain references a missing record.");
                }

                if (record.Type == WalRecordType.PageChange)
                {
                    await recovery.UndoRecoveryPageContextImageAsync(recoveryPageContext: source with { Payload = record.Payload.ToArray() }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                    var compensation = await wal!.AppendWalAppendContextAsync(walAppendContext: new WalAppendContext { Record = new WalRecord { TransactionId = transaction.Key, Type = WalRecordType.Compensation, PreviousLsn = record.PreviousLsn, Payload = record.Payload } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                    await wal!.FlushWalAppendContextAsync(logSequenceNumber: compensation.Record!.Lsn.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                    actions++;

                    if (actions >= source.MaximumActions)
                    {
                        return false;
                    }
                }

                previous = record.PreviousLsn;
            }

            var rollback = await wal!.AppendWalAppendContextAsync(walAppendContext: new WalAppendContext { Record = new WalRecord { TransactionId = transaction.Key, Type = WalRecordType.Rollback, PreviousLsn = current?.Lsn ?? default, Payload = ReadOnlyMemory<byte>.Empty } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await wal!.FlushWalAppendContextAsync(logSequenceNumber: rollback.Record!.Lsn.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        return true;
    });
}