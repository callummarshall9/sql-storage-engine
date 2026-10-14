// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.Services.Processings.RecoveryAnalyses;
internal sealed partial class RecoveryAnalysisProcessingService
{
    private static void ValidateRecoveryAnalysis(RecoveryAnalysis recoveryAnalysis)
    {
        ArgumentNullException.ThrowIfNull(argument: recoveryAnalysis);
        ArgumentNullException.ThrowIfNull(argument: recoveryAnalysis.Records);

        if (recoveryAnalysis.Checkpoint is not null)
        {
            ArgumentNullException.ThrowIfNull(argument: recoveryAnalysis.Checkpoint.ActiveTransactions);
            ArgumentNullException.ThrowIfNull(argument: recoveryAnalysis.Checkpoint.DirtyPages);
        }
    }

    private static void ValidateRecords(WalRecord[] records)
    {
        if (records.Any(predicate: record => record is null))
        {
            throw new ArgumentException(message: "WAL records cannot contain null entries.", paramName: nameof(records));
        }
    }
}