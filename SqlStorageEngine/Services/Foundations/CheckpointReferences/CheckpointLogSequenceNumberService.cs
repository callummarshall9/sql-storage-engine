// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.Services.Foundations.CheckpointReferences;
internal sealed partial class CheckpointLogSequenceNumberService : ICheckpointLogSequenceNumberService
{
    private readonly CheckpointReferenceState state = new()
    {
        Sync = new object ()
    };
    public LogSequenceNumber? ReadCheckpointLogSequenceNumber() =>
        TryCatch(operation: () =>
    {
        lock (state.Sync)
        {
            return state.Latest is null ? (LogSequenceNumber? )null : new LogSequenceNumber
            {
                Value = state.Latest.Value
            };
        }
    });

    public void ConfigureCheckpointPublicationFailure(bool failPublish) =>
        TryCatch(operation: () =>
    {
        ValidateCheckpointFailureConfiguration(failPublish: failPublish);

        lock (state.Sync)
        {
            state.FailPublish = failPublish;
        }
    });

    public ValueTask PublishCheckpointLogSequenceNumberAsync(ulong checkpointLsn, CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        lock (state.Sync)
        {
            ValidateCheckpointPublication(checkpointLsn: checkpointLsn, cancellationToken: cancellationToken);
            state.Latest = checkpointLsn;
            return ValueTask.CompletedTask;
        }
    });
}