// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Exceptions;

namespace sql_storage_engine.Services.Foundations.CheckpointReferences;
internal sealed partial class CheckpointLogSequenceNumberService
{
    private void ValidateCheckpointFailureConfiguration(bool failPublish)
    {
        lock (state.Sync)
        {
            if (state.Latest is not null && state.Latest.Value == 0)
            {
                throw new ArgumentException(message: "Invalid checkpoint publication state.");
            }
        }
    }

    private void ValidateCheckpointPublication(ulong checkpointLsn, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (state.FailPublish)
        {
            throw new CheckpointPublicationDependencyException(message: "checkpoint publication");
        }

        if (checkpointLsn == 0)
        {
            throw new ArgumentException(message: "Checkpoint LSN must be nonzero.");
        }
    }
}