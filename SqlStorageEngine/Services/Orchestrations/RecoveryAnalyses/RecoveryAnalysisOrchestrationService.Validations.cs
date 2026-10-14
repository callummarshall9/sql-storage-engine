// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Logging;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Orchestrations.RecoveryAnalyses;
internal sealed partial class RecoveryAnalysisOrchestrationService
{
    private static void ValidateRecoveryAnalysis(RecoveryAnalysis recoveryAnalysis, Guid expectedDatabaseId, ulong expectedTimeline, ReadOnlySpan<byte> recordBytes)
    {
        ArgumentNullException.ThrowIfNull(argument: recoveryAnalysis);
        ArgumentNullException.ThrowIfNull(argument: recoveryAnalysis.Segment);

        if (recoveryAnalysis.Segment.DatabaseId.Value != expectedDatabaseId)
        {
            throw new StorageCorruptionException(message: "WAL belongs to a different database.");
        }

        if (recoveryAnalysis.Segment.Timeline != expectedTimeline)
        {
            throw new StorageCorruptionException(message: "WAL belongs to a different timeline.");
        }
    }

    private static void ValidateCheckpoint(RecoveryAnalysis recoveryAnalysis)
    {
        ArgumentNullException.ThrowIfNull(argument: recoveryAnalysis);
        ArgumentNullException.ThrowIfNull(argument: recoveryAnalysis.Checkpoint);
        ArgumentNullException.ThrowIfNull(argument: recoveryAnalysis.Records);
    }
}