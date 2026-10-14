// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers.Binary;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.TransactionDirectories;
internal sealed partial class TransactionDirectoryService
{
    private byte[] EncodeMetadata(TransactionMetadata transactionMetadata)
    {
        var bytes = new byte[TransactionMetadataFormat.EncodedLength];
        "SQLTXMD1"u8.CopyTo(destination: bytes);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: bytes.AsSpan(start: 8, length: 2), value: TransactionMetadataFormat.Version);

        var kind = transactionMetadata.State switch
        {
            null => (ushort)1,
            StorageTransactionState.Committed => (ushort)2,
            StorageTransactionState.Aborted => (ushort)3,
            _ => throw new ArgumentException(message: "Transaction metadata decision is invalid.", paramName: nameof(transactionMetadata))
        };

        BinaryPrimitives.WriteUInt16LittleEndian(destination: bytes.AsSpan(start: 10, length: 2), value: kind);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: bytes.AsSpan(start: 12, length: 4), value: TransactionMetadataFormat.PayloadLength);
        transactionMetadata.Identity.DatabaseId.Value.TryWriteBytes(destination: bytes.AsSpan(start: 16, length: 16), bigEndian: true, bytesWritten: out _);
        transactionMetadata.Identity.Value.TryWriteBytes(destination: bytes.AsSpan(start: 32, length: 16), bigEndian: true, bytesWritten: out _);

        transactionDirectoryBroker.HashSnapshotBytes(bytes: bytes[..48])
            .CopyTo(array: bytes, index: 48);

        return bytes;
    }

    private TransactionMetadata DecodeMetadata(byte[] bytes, bool receipt)
    {
        if (bytes.Length != TransactionMetadataFormat.EncodedLength || !bytes.AsSpan(start: 0, length: 8)
            .SequenceEqual(other: "SQLTXMD1"u8) || BinaryPrimitives.ReadUInt16LittleEndian(source: bytes.AsSpan(start: 8, length: 2)) != TransactionMetadataFormat.Version || BinaryPrimitives.ReadUInt32LittleEndian(source: bytes.AsSpan(start: 12, length: 4)) != TransactionMetadataFormat.PayloadLength)
        {
            throw new StorageFormatException(message: "Transaction metadata envelope is invalid.");
        }

        if (!bytes.AsSpan(start: 48, length: 32)
            .SequenceEqual(other: transactionDirectoryBroker.HashSnapshotBytes(bytes: bytes[..48])))
        {
            throw new StorageFormatException(message: "Transaction metadata checksum is invalid.");
        }

        var state = BinaryPrimitives.ReadUInt16LittleEndian(source: bytes.AsSpan(start: 10, length: 2)) switch
        {
            1 => (StorageTransactionState?)null,
            2 => StorageTransactionState.Committed,
            3 => StorageTransactionState.Aborted,
            _ => throw new StorageFormatException(message: "Transaction metadata kind is invalid.")
        };

        if (receipt != state.HasValue)
        {
            throw new StorageFormatException(message: "Transaction metadata type is invalid.");
        }

        var databaseId = new Guid(b: bytes.AsSpan(start: 16, length: 16), bigEndian: true);
        var value = new Guid(b: bytes.AsSpan(start: 32, length: 16), bigEndian: true);

        if (databaseId == Guid.Empty || value == Guid.Empty)
        {
            throw new StorageFormatException(message: "Transaction metadata identity is invalid.");
        }

        return new TransactionMetadata
        {
            Identity = new sql_storage_engine.Models.StorageTransactionIdentity { DatabaseId = new DatabaseId { Value = databaseId }, Value = value },
            State = state
        };
    }
}