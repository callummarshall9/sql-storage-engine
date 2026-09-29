using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Logging;

public sealed class MemoryCheckpointReference : ICheckpointReference
{
    public LogSequenceNumber? LatestCheckpointLsn { get; private set; }
    public bool FailPublish { get; set; }
    public ValueTask PublishAsync(LogSequenceNumber checkpointLsn, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); if (FailPublish) return ValueTask.FromException(new IOException("checkpoint publication")); LatestCheckpointLsn = checkpointLsn; return ValueTask.CompletedTask; }
}
