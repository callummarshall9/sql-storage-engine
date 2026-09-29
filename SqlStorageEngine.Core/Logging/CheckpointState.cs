using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Logging;

public sealed record CheckpointState(LogSequenceNumber SafeRecoveryLsn,
    IReadOnlyDictionary<TransactionId, LogSequenceNumber> ActiveTransactions,
    IReadOnlyDictionary<PageId, LogSequenceNumber> DirtyPages);
