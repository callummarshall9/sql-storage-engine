// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Logging;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.CheckpointStates;
internal sealed partial class CheckpointStateCodecProcessingService : ICheckpointStateCodecProcessingService
{
    public byte[] WriteCheckpointState(CheckpointState checkpointState) =>
        TryCatch(operation: () =>
    {
        ValidateCheckpointState(checkpointState: checkpointState);
        var bytes = new byte[checked(24 + (checkpointState.ActiveTransactions.Count + checkpointState.DirtyPages.Count) * 16)];
        BinaryPrimitives.WriteUInt16LittleEndian(destination: bytes, value: CheckpointFormat.Version);
        BinaryPrimitives.WriteUInt64LittleEndian(destination: bytes.AsSpan(start: 8), value: checkpointState.SafeRecoveryLsn.Value);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: bytes.AsSpan(start: 16), value: checked((uint)checkpointState.ActiveTransactions.Count));
        BinaryPrimitives.WriteUInt32LittleEndian(destination: bytes.AsSpan(start: 20), value: checked((uint)checkpointState.DirtyPages.Count));
        var offset = 24;

        foreach (var pair in checkpointState.ActiveTransactions.OrderBy(keySelector: pair => pair.Key.Value))
        {
            BinaryPrimitives.WriteUInt64LittleEndian(destination: bytes.AsSpan(start: offset), value: pair.Key.Value);
            BinaryPrimitives.WriteUInt64LittleEndian(destination: bytes.AsSpan(start: offset + 8), value: pair.Value.Value);
            offset += 16;
        }

        foreach (var pair in checkpointState.DirtyPages.OrderBy(keySelector: pair => pair.Key.Value))
        {
            BinaryPrimitives.WriteUInt64LittleEndian(destination: bytes.AsSpan(start: offset), value: pair.Key.Value);
            BinaryPrimitives.WriteUInt64LittleEndian(destination: bytes.AsSpan(start: offset + 8), value: pair.Value.Value);
            offset += 16;
        }

        return bytes;
    });

    public CheckpointState ReadCheckpointState(ReadOnlySpan<byte> source) =>
        TryCatch(operation: static context =>
    {
        ValidateCheckpointStateSource(source: context.source);
        var active = (int)BinaryPrimitives.ReadUInt32LittleEndian(source: context.source[16..]);
        var dirty = (int)BinaryPrimitives.ReadUInt32LittleEndian(source: context.source[20..]);
        Dictionary<TransactionId, LogSequenceNumber> transactions = [];
        Dictionary<PageId, LogSequenceNumber> pages = [];
        var offset = 24;

        for (var index = 0; index < active; index++, offset += 16)
        {
            if (!transactions.TryAdd(key: new sql_storage_engine.Models.Identifiers.TransactionId { Value = BinaryPrimitives.ReadUInt64LittleEndian(source: context.source[offset..]) }, value: new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = BinaryPrimitives.ReadUInt64LittleEndian(source: context.source[(offset + 8)..]) }))
            {
                throw new StorageFormatException("Duplicate checkpoint transaction.");
            }
        }

        for (var index = 0; index < dirty; index++, offset += 16)
        {
            if (!pages.TryAdd(key: new sql_storage_engine.Models.Identifiers.PageId { Value = BinaryPrimitives.ReadUInt64LittleEndian(source: context.source[offset..]) }, value: new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = BinaryPrimitives.ReadUInt64LittleEndian(source: context.source[(offset + 8)..]) }))
            {
                throw new StorageFormatException("Duplicate checkpoint page.");
            }
        }

        return new CheckpointState
        {
            SafeRecoveryLsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
            {
                Value = BinaryPrimitives.ReadUInt64LittleEndian(source: context.source[8..])
            },
            ActiveTransactions = new ReadOnlyDictionary<TransactionId, LogSequenceNumber>(transactions),
            DirtyPages = new ReadOnlyDictionary<PageId, LogSequenceNumber>(pages)
        };
    }, context: new ReadableWalFormatContext { source = source });

    public ulong ReadCheckpointStateRetention(CheckpointState checkpointState) =>
        TryCatch(operation: () =>
    {
        ValidateCheckpointState(checkpointState: checkpointState);
        var result = checkpointState.SafeRecoveryLsn.Value;

        foreach (var lsn in checkpointState.ActiveTransactions.Values)
        {
            if (result == 0 || lsn.Value < result)
            {
                result = lsn.Value;
            }
        }

        return result;
    });
}