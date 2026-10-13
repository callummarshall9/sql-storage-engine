// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Logging;
using sql_storage_engine.Services.Foundations.CheckpointReferences;

namespace sql_storage_engine.Exposures.Logging;
public sealed class CheckpointReferenceExposure : ICheckpointReference
{
    private readonly CheckpointLogSequenceNumberService service = new();
    public LogSequenceNumber? ReadLatestCheckpointLsn() =>
        service.ReadCheckpointLogSequenceNumber();

    public void SetPublicationFailure(bool failPublish) =>
        service.ConfigureCheckpointPublicationFailure(failPublish: failPublish);

    public ValueTask PublishAsync(LogSequenceNumber logSequenceNumber, CancellationToken cancellationToken = default) =>
        service.PublishCheckpointLogSequenceNumberAsync(checkpointLsn: logSequenceNumber.Value, cancellationToken: cancellationToken);
}