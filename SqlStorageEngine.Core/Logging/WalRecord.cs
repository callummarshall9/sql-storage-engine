using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Logging;

public sealed record WalRecord(LogSequenceNumber Lsn, LogSequenceNumber PreviousLsn,
    TransactionId TransactionId, WalRecordType Type, ReadOnlyMemory<byte> Payload);
