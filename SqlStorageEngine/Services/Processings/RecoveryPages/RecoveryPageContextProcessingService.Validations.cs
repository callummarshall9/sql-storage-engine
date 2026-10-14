// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.Services.Processings.RecoveryPages;
internal sealed partial class RecoveryPageContextProcessingService
{
    private static void ValidateRecoveryPageContext(RecoveryPageContext recoveryPageContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: recoveryPageContext);
        cancellationToken.ThrowIfCancellationRequested();
    }
}