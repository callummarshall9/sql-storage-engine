// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.Services.Orchestrations.RecoveryAnalyses;
internal interface IRecoveryAnalysisOrchestrationService
{
    RecoveryAnalysis AnalyzeRecoveryAnalysis(RecoveryAnalysis recoveryAnalysis, Guid expectedDatabaseId, ulong expectedTimeline, ReadOnlySpan<byte> recordBytes);

    RecoveryAnalysis ResumeRecoveryAnalysis(RecoveryAnalysis recoveryAnalysis);
}