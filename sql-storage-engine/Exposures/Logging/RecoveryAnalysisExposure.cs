// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Services.Processings.RecoveryAnalyses;
using sql_storage_engine.Services.Processings.WalReadResults;
using sql_storage_engine.Services.Orchestrations.RecoveryAnalyses;

namespace sql_storage_engine.Exposures.Logging;
public static class RecoveryAnalysisExposure
{
    private static readonly IRecoveryAnalysisOrchestrationService service = new RecoveryAnalysisOrchestrationService(walRecords: new WalRecordReadProcessingService(walRecords: new sql_storage_engine.Services.Foundations.WalRecords.WalRecordService()), recovery: new RecoveryAnalysisProcessingService());
    public static RecoveryAnalysis Analyze(WalSegmentHeader walSegmentHeader, DatabaseId databaseId, ulong expectedTimeline, ReadOnlySpan<byte> recordBytes) =>
        service.AnalyzeRecoveryAnalysis(recoveryAnalysis: new RecoveryAnalysis { Segment = walSegmentHeader }, expectedDatabaseId: databaseId.Value, expectedTimeline: expectedTimeline, recordBytes: recordBytes);

    public static RecoveryAnalysis ResumeFromCheckpoint(CheckpointState checkpointState, IReadOnlyList<WalRecord> recordsAfterCheckpoint) =>
        service.ResumeRecoveryAnalysis(recoveryAnalysis: new RecoveryAnalysis { Checkpoint = checkpointState, Records = recordsAfterCheckpoint });
}