using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Logging;

public interface ICheckpointReference
{
    LogSequenceNumber? LatestCheckpointLsn { get; }
    ValueTask PublishAsync(LogSequenceNumber checkpointLsn, CancellationToken cancellationToken = default);
}
