// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.Services.Orchestrations.RecoveryPages;
internal sealed partial class RecoveryPageContextOrchestrationService
{
    private static void ValidateAnalysis(RecoveryPageContext recoveryPageContext)
    {
        ArgumentNullException.ThrowIfNull(argument: recoveryPageContext);
        ArgumentNullException.ThrowIfNull(argument: recoveryPageContext.Analysis);
        ArgumentNullException.ThrowIfNull(argument: recoveryPageContext.Analysis.Records);
        ArgumentNullException.ThrowIfNull(argument: recoveryPageContext.Analysis.Transactions);
    }

    private static void ValidatePayload(RecoveryPageContext recoveryPageContext)
    {
        ArgumentNullException.ThrowIfNull(argument: recoveryPageContext);
        ArgumentNullException.ThrowIfNull(argument: recoveryPageContext.Payload);
    }

    private static void ValidateRedo(RecoveryPageContext recoveryPageContext, CancellationToken cancellationToken)
    {
        ValidateAnalysis(recoveryPageContext: recoveryPageContext);
        ArgumentNullException.ThrowIfNull(argument: recoveryPageContext.ProviderSource);
        ArgumentNullException.ThrowIfNull(argument: recoveryPageContext.PageSize);
        ArgumentNullException.ThrowIfNull(argument: recoveryPageContext.Read);
        ArgumentNullException.ThrowIfNull(argument: recoveryPageContext.Write);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateUndo(RecoveryPageContext recoveryPageContext, CancellationToken cancellationToken)
    {
        ValidatePayload(recoveryPageContext: recoveryPageContext);
        ArgumentNullException.ThrowIfNull(argument: recoveryPageContext.ProviderSource);
        ArgumentNullException.ThrowIfNull(argument: recoveryPageContext.Write);
        cancellationToken.ThrowIfCancellationRequested();
    }
}