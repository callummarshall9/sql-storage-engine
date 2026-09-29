using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.Logging;

public sealed record RecoveryAnalysis(IReadOnlyDictionary<TransactionId, TransactionState> Transactions,
    IReadOnlyDictionary<PageId, LogSequenceNumber> DirtyPages, IReadOnlyList<WalRecord> Records,
    int ValidLength, bool TruncatedTail);
