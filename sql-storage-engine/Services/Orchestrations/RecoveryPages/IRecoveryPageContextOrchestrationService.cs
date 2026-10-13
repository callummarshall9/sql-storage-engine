// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.Services.Orchestrations.RecoveryPages;
internal interface IRecoveryPageContextOrchestrationService
{
    ValueTask RedoRecoveryPageContextAsync(RecoveryPageContext recoveryPageContext, CancellationToken cancellationToken);

    RecoveryPageContext ReadRecoveryPageContextChange(RecoveryPageContext recoveryPageContext);

    ValueTask UndoRecoveryPageContextImageAsync(RecoveryPageContext recoveryPageContext, CancellationToken cancellationToken);

    RecoveryPageContext ReadRecoveryPageContextAnalysis(RecoveryPageContext recoveryPageContext);
}