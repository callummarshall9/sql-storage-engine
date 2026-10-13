// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.Services.Processings.RecoveryAnalyses;
internal interface IRecoveryAnalysisProcessingService
{
    RecoveryAnalysis PrepareRecoveryAnalysis(RecoveryAnalysis recoveryAnalysis);
}