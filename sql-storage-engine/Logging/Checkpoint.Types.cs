// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Logging;
public interface ICheckpointReference
{
    LogSequenceNumber? ReadLatestCheckpointLsn();

    ValueTask PublishAsync(LogSequenceNumber checkpointLsn, CancellationToken cancellationToken = default);
}