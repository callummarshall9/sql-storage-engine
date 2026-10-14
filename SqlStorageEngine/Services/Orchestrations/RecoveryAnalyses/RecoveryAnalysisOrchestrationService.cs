// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Services.Processings.RecoveryAnalyses;
using sql_storage_engine.Services.Processings.WalReadResults;

namespace sql_storage_engine.Services.Orchestrations.RecoveryAnalyses;
internal sealed partial class RecoveryAnalysisOrchestrationService(IWalRecordReadProcessingService walRecords, IRecoveryAnalysisProcessingService recovery) : IRecoveryAnalysisOrchestrationService
{
    public RecoveryAnalysis AnalyzeRecoveryAnalysis(RecoveryAnalysis recoveryAnalysis, Guid expectedDatabaseId, ulong expectedTimeline, ReadOnlySpan<byte> recordBytes) =>
        TryCatch(operation: context =>
    {
        ValidateRecoveryAnalysis(recoveryAnalysis: context.recoveryAnalysis, expectedDatabaseId: context.expectedDatabaseId, expectedTimeline: context.expectedTimeline, recordBytes: context.recordBytes);
        var read = walRecords.ReadWalReadResult(source: context.recordBytes);
        return recovery.PrepareRecoveryAnalysis(recoveryAnalysis: context.recoveryAnalysis with { Records = read.Records, ValidLength = read.ValidLength, TruncatedTail = read.HasIncompleteTail });
    }, context: new ReadableRecoveryAnalysisContext { recoveryAnalysis = recoveryAnalysis, expectedDatabaseId = expectedDatabaseId, expectedTimeline = expectedTimeline, recordBytes = recordBytes });

    public RecoveryAnalysis ResumeRecoveryAnalysis(RecoveryAnalysis recoveryAnalysis) =>
        TryCatch(operation: () =>
    {
        ValidateCheckpoint(recoveryAnalysis: recoveryAnalysis);
        return recovery.PrepareRecoveryAnalysis(recoveryAnalysis: recoveryAnalysis);
    });
}