// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Services.Foundations.CheckpointReferences;
internal interface ICheckpointLogSequenceNumberService
{
    LogSequenceNumber? ReadCheckpointLogSequenceNumber();

    void ConfigureCheckpointPublicationFailure(bool failPublish);

    ValueTask PublishCheckpointLogSequenceNumberAsync(ulong checkpointLsn, CancellationToken cancellationToken);
}