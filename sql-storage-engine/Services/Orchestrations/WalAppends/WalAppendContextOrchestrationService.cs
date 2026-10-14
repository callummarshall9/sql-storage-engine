// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Services.Processings.WalDevices;
using sql_storage_engine.Services.Processings.WalRecordContexts;
using sql_storage_engine.Services.Processings.CheckpointStates;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Orchestrations.WalAppends;
internal sealed partial class WalAppendContextOrchestrationService : IWalAppendContextOrchestrationService
{
    private readonly WalDeviceProcessingService device;
    private readonly WalRecordWalAppendContextCodecProcessingService records;
    private readonly CheckpointStateCodecProcessingService checkpoints;
    private readonly WalAppendContext state;
    private ulong durableLsn;
    internal WalAppendContextOrchestrationService(WalDeviceProcessingService device, WalRecordWalAppendContextCodecProcessingService records, CheckpointStateCodecProcessingService checkpoints, WalDeviceConfiguration walDeviceConfiguration)
    {
        this.device = device;
        this.records = records;
        this.checkpoints = checkpoints;
        state = new WalAppendContext
        {
            MaximumSegmentBytes = walDeviceConfiguration.MaximumSegmentBytes
        };
    }

    internal static WalAppendContextOrchestrationService CreateWalAppendContextOrchestrationService(WalDeviceConfiguration walDeviceConfiguration) =>
        new WalAppendContextOrchestrationService(device: WalDeviceProcessingService.CreateWalDeviceProcessingService(walDeviceConfiguration: walDeviceConfiguration), records: WalRecordWalAppendContextCodecProcessingService.CreateWalRecordWalAppendContextCodecProcessingService(), checkpoints: new CheckpointStateCodecProcessingService(), walDeviceConfiguration: walDeviceConfiguration);

    internal async ValueTask<WalAppendContextOrchestrationService> OpenWalAppendContextOrchestrationServiceAsync(CancellationToken cancellationToken)
    {
        await TryCatch(operation: async () =>
        {
            ValidateOpen(cancellationToken: cancellationToken);

            await OpenCoreAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        })
            .ConfigureAwait(continueOnCapturedContext: false);

        return this;
    }

    public ValueTask<WalAppendContext> AppendWalAppendContextAsync(WalAppendContext walAppendContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateAppend(walAppendContext: walAppendContext, cancellationToken: cancellationToken);

        var record = await AppendCoreAsync(walRecord: walAppendContext.Record!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return walAppendContext with
        {
            Record = record
        };
    });

    public ValueTask FlushWalAppendContextAsync(ulong logSequenceNumber, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateFlush(logSequenceNumber: logSequenceNumber, cancellationToken: cancellationToken);

        await FlushCoreAsync(logSequenceNumber: logSequenceNumber, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public WalAppendContext ReadWalAppendContext() =>
        TryCatch(operation: () =>
    {
        ValidateState();

        return state with
        {
            DurableLsn = Volatile.Read(location: ref durableLsn)
        };
    });

    public ValueTask GuardWalAppendContextAsync(ulong logSequenceNumber, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateGuard(logSequenceNumber: logSequenceNumber, cancellationToken: cancellationToken);

        if (logSequenceNumber != 0)
        {
            await FlushCoreAsync(logSequenceNumber: logSequenceNumber, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
    });

    public WalAppendContext CreateWalAppendContextTransaction(ulong transactionId) =>
        TryCatch(operation: () =>
    {
        ValidateTransactionId(transactionId: transactionId);

        return new WalAppendContext
        {
            TransactionId = new TransactionId
            {
                Value = transactionId
            },
            TransactionState = sql_storage_engine.Transactions.TransactionState.Active
        };
    });

    public WalAppendContext ReadWalAppendContextTransaction(WalAppendContext walAppendContext) =>
        TryCatch(operation: () =>
    {
        ValidateTransaction(walAppendContext: walAppendContext);

        return walAppendContext with
        {
        };
    });

    public void CheckWalAppendContextTransaction(WalAppendContext walAppendContext) =>
        TryCatch(operation: () =>
    {
        ValidateActiveTransaction(walAppendContext: walAppendContext);
    });

    public ValueTask<ulong> AppendWalAppendContextChangeAsync(WalAppendContext walAppendContext, ReadOnlyMemory<byte> physicalChange, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateChange(walAppendContext: walAppendContext, physicalChange: physicalChange, cancellationToken: cancellationToken);

        var record = await AppendCoreAsync(walRecord: new WalRecord { TransactionId = walAppendContext.TransactionId, Type = sql_storage_engine.Logging.WalRecordType.PageChange, PreviousLsn = walAppendContext.PreviousLsn, Payload = physicalChange }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        walAppendContext.PreviousLsn = record.Lsn;
        return record.Lsn.Value;
    });

    public ValueTask CommitWalAppendContextTransactionAsync(WalAppendContext walAppendContext, Func<ValueTask>? beforeResponse, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCommit(walAppendContext: walAppendContext, beforeResponse: beforeResponse, cancellationToken: cancellationToken);

        try
        {
            var commit = await AppendCoreAsync(walRecord: new WalRecord { TransactionId = walAppendContext.TransactionId, Type = sql_storage_engine.Logging.WalRecordType.Commit, PreviousLsn = walAppendContext.PreviousLsn, Payload = ReadOnlyMemory<byte>.Empty }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await FlushCoreAsync(logSequenceNumber: commit.Lsn.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            walAppendContext.TransactionState = sql_storage_engine.Transactions.TransactionState.Committed;

            if (beforeResponse is not null)
            {
                await beforeResponse()
            .ConfigureAwait(continueOnCapturedContext: false);
            }
        }
        catch
        {
            if (walAppendContext.TransactionState != sql_storage_engine.Transactions.TransactionState.Committed)
            {
                walAppendContext.TransactionState = sql_storage_engine.Transactions.TransactionState.Failed;
            }

            throw;
        }
    });

    public ValueTask<ulong> AppendWalAppendContextCheckpointAsync(WalAppendContext walAppendContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCheckpoint(walAppendContext: walAppendContext, cancellationToken: cancellationToken);
        var payload = checkpoints.WriteCheckpointState(checkpointState: walAppendContext.Checkpoint!);

        var record = await AppendCoreAsync(walRecord: new WalRecord { TransactionId = new TransactionId { Value = 1 }, Type = sql_storage_engine.Logging.WalRecordType.Checkpoint, Payload = payload }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await FlushCoreAsync(logSequenceNumber: record.Lsn.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await device.PublishWalAppendContextCheckpointAsync(publication: walAppendContext.PublishCheckpoint!, logSequenceNumber: record.Lsn.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return record.Lsn.Value;
    });

    private async ValueTask OpenCoreAsync(CancellationToken cancellationToken)
    {
        if (state.MaximumSegmentBytes < WalFormatConstants.RecordHeaderLength)
        {
            throw new ArgumentOutOfRangeException(nameof(state.MaximumSegmentBytes));
        }

        var bytes = await device.ReadWalAppendContextBytesAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var result = records.ReadWalAppendContext(walAppendContext: new WalAppendContext { Source = bytes })
            .ReadResult!;

        if (result.HasIncompleteTail)
        {
            device.TruncateWalAppendContext(length: result.ValidLength);
        }

        var segment = 0UL;
        var segmentBytes = 0;

        foreach (var record in result.Records)
        {
            var length = records.WriteWalAppendContext(walAppendContext: new WalAppendContext { Record = record })
            .EncodedRecord!.Length;

            if (segmentBytes > 0 && segmentBytes + length > state.MaximumSegmentBytes)
            {
                segment++;
                segmentBytes = 0;
            }

            segmentBytes += length;
        }

        state.Length = result.ValidLength;
        state.SegmentBytes = segmentBytes;
        state.SegmentNumber = segment;
        state.IsOpen = true;
    }

    private async ValueTask<WalRecord> AppendCoreAsync(WalRecord walRecord, CancellationToken cancellationToken)
    {
        await device.EnterWalAppendContextAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            var provisional = walRecord with
            {
                Lsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
                {
                    Value = checked((ulong)state.Length + 1)
                }
            };

            var bytes = records.WriteWalAppendContext(walAppendContext: new WalAppendContext { Record = provisional })
            .EncodedRecord!;

            if (bytes.Length > state.MaximumSegmentBytes)
            {
                throw new StorageResourceExhaustedException("WAL record exceeds segment capacity.");
            }

            if (state.SegmentBytes > 0 && state.SegmentBytes + bytes.Length > state.MaximumSegmentBytes)
            {
                await device.RollWalAppendContextAsync(segmentNumber: ++state.SegmentNumber, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                state.SegmentBytes = 0;
            }

            await device.WriteWalAppendContextBytesAsync(offset: state.Length, source: bytes, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            state.Length = checked(state.Length + bytes.Length);
            state.SegmentBytes = checked(state.SegmentBytes + bytes.Length);
            return provisional;
        }
        finally
        {
            device.ExitWalAppendContext();
        }
    }

    private async ValueTask FlushCoreAsync(ulong logSequenceNumber, CancellationToken cancellationToken)
    {
        await device.EnterWalAppendContextAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            if (logSequenceNumber == 0 || logSequenceNumber > checked((ulong)state.Length))
            {
                throw new ArgumentOutOfRangeException(nameof(logSequenceNumber));
            }

            await device.FlushWalAppendContextAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            if (logSequenceNumber > durableLsn)
            {
                Volatile.Write(location: ref durableLsn, value: logSequenceNumber);
            }
        }
        finally
        {
            device.ExitWalAppendContext();
        }
    }
}