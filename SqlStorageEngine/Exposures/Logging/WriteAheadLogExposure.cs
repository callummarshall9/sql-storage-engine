// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Models.Identifiers;
using cCoder.CodeAnalysis.Exposures;
using sql_storage_engine.Services.Coordinations.WalRecovery;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Logging;
using sql_storage_engine.Transactions;
using sql_storage_engine.Buffers;

namespace sql_storage_engine.Exposures.Logging;
public sealed class WriteAheadLogExposure : ICompositionExposure, IPageFlushGuard
{
    private readonly WalAppendContextCoordinationService service;
    private readonly WalAppendContext? transactionContext;
    private readonly WalAppendContext? recoveryContext;
    private WriteAheadLogExposure(WalAppendContextCoordinationService service)
    {
        this.service = service;
    }

    public static async ValueTask<WriteAheadLogExposure> OpenAsync(IWalDevice device, int maximumSegmentBytes = 16 * 1024 * 1024, CancellationToken cancellationToken = default) =>
        new WriteAheadLogExposure(service: await WalAppendContextCoordinationService.CreateWalAppendContextCoordinationService(walDeviceConfiguration: new WalDeviceConfiguration { ProviderSource = device, MaximumSegmentBytes = maximumSegmentBytes, Length = () => device.Length, Read = (offset, destination, token) => device.ReadAsync(offset: offset, destination: destination, cancellationToken: token), Write = (offset, source, token) => device.WriteAsync(offset: offset, source: source, cancellationToken: token), Flush = token => device.FlushAsync(cancellationToken: token), Roll = (segment, token) => device.RollSegmentAsync(segmentNumber: segment, cancellationToken: token), Truncate = length => device.Truncate(length: length) })
            .OpenWalAppendContextCoordinationServiceAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false));

    public LogSequenceNumber ReadDurableLsn() =>
        new LogSequenceNumber
    {
        Value = service.ReadWalAppendContext()
            .DurableLsn
    };

    public ulong ReadCurrentSegmentNumber() =>
        service.ReadWalAppendContext()
            .SegmentNumber;

    public async ValueTask<WalRecord> AppendAsync(TransactionId transactionId, WalRecordType walRecordType, LogSequenceNumber logSequenceNumber, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default) =>
        (await service.AppendWalAppendContextAsync(walAppendContext: new WalAppendContext { Record = new WalRecord { TransactionId = transactionId, Type = walRecordType, PreviousLsn = logSequenceNumber, Payload = payload } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Record!;

    public ValueTask FlushThroughAsync(LogSequenceNumber logSequenceNumber, CancellationToken cancellationToken = default) =>
        service.FlushWalAppendContextAsync(logSequenceNumber: logSequenceNumber.Value, cancellationToken: cancellationToken);

    public ValueTask EnsureCanFlushAsync(PageId pageId, LogSequenceNumber logSequenceNumber, CancellationToken cancellationToken = default) =>
        service.GuardWalAppendContextAsync(logSequenceNumber: logSequenceNumber.Value, cancellationToken: cancellationToken);

    public WriteAheadLogExposure CreateWriteAheadLogExposureTransaction(TransactionId transactionId) =>
        new WriteAheadLogExposure(service: service, walAppendContext: service.CreateWalAppendContextTransaction(transactionId: transactionId.Value));
    private WriteAheadLogExposure(WalAppendContextCoordinationService service, WalAppendContext walAppendContext)
    {
        this.service = service;
        transactionContext = walAppendContext;
    }

    public TransactionId ReadTransactionId() =>
        service.ReadWalAppendContextTransaction(walAppendContext: transactionContext!)
            .TransactionId;

    public TransactionState ReadTransactionState() =>
        service.ReadWalAppendContextTransaction(walAppendContext: transactionContext!)
            .TransactionState;

    public async ValueTask<LogSequenceNumber> AppendChangeAsync(ReadOnlyMemory<byte> physicalChange, CancellationToken cancellationToken = default) =>
        new LogSequenceNumber
    {
        Value = await service.AppendWalAppendContextChangeAsync(walAppendContext: transactionContext!, physicalChange: physicalChange, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)
    };

    public ValueTask CommitAsync(Func<ValueTask>? beforeResponse = null, CancellationToken cancellationToken = default) =>
        service.CommitWalAppendContextTransactionAsync(walAppendContext: transactionContext!, beforeResponse: beforeResponse, cancellationToken: cancellationToken);

    public void EnsureActive() =>
        service.CheckWalAppendContextTransaction(walAppendContext: transactionContext!);

    public async ValueTask<LogSequenceNumber> CreateCheckpointLogSequenceNumberAsync(CheckpointState checkpointState, ICheckpointReference reference, CancellationToken cancellationToken = default) =>
        new LogSequenceNumber
    {
        Value = await service.AppendWalAppendContextCheckpointAsync(walAppendContext: new WalAppendContext { Checkpoint = checkpointState, CheckpointProvider = reference, PublishCheckpoint = (lsn, token) => reference.PublishAsync(checkpointLsn: new LogSequenceNumber { Value = lsn }, cancellationToken: token) }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)
    };

    public static ValueTask ApplyRecoveryRedoAsync(IPageStoreExposure pageStore, RecoveryAnalysis recoveryAnalysis, CancellationToken cancellationToken = default) =>
        new WriteAheadLogExposure(service: WalAppendContextCoordinationService.CreateWalAppendContextCoordinationServiceReplay(), pageStore: pageStore, recoveryAnalysis: recoveryAnalysis, maximumActions: int.MaxValue).RedoAsync(cancellationToken: cancellationToken);

    public ValueTask<bool> ApplyRecoveryUndoAsync(IPageStoreExposure pageStore, RecoveryAnalysis recoveryAnalysis, int maximumActions = int.MaxValue, CancellationToken cancellationToken = default) =>
        new WriteAheadLogExposure(service: service, pageStore: pageStore, recoveryAnalysis: recoveryAnalysis, maximumActions: maximumActions).UndoAsync(cancellationToken: cancellationToken);
    private WriteAheadLogExposure(WalAppendContextCoordinationService service, IPageStoreExposure pageStore, RecoveryAnalysis recoveryAnalysis, int maximumActions)
    {
        this.service = service;
        recoveryContext = new WalAppendContext
        {
            Recovery = new RecoveryPageContext
            {
                ProviderSource = pageStore,
                Analysis = recoveryAnalysis,
                MaximumActions = maximumActions,
                PageSize = () => pageStore.GetPageSize(),
                Read = (pageId, destination, token) => pageStore.ReadAsync(pageId: new PageId { Value = pageId }, destination: destination, cancellationToken: token),
                Write = (pageId, source, token) => pageStore.WriteAsync(pageId: new PageId { Value = pageId }, source: source, cancellationToken: token)
            }
        };
    }

    private ValueTask RedoAsync(CancellationToken cancellationToken) =>
        service.RedoWalAppendContextAsync(walAppendContext: recoveryContext!, cancellationToken: cancellationToken);

    private ValueTask<bool> UndoAsync(CancellationToken cancellationToken) =>
        service.UndoWalAppendContextAsync(walAppendContext: recoveryContext!, cancellationToken: cancellationToken);
}