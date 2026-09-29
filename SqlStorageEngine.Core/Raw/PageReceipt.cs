using SqlExecutionEngine.Storage.Abstractions;

namespace SqlStorageEngine.Core;

internal sealed record PageReceipt(ByteString Fingerprint, CommitReceipt Outcome);
