// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.Services.Foundations.RecoveryPages;
internal sealed partial class RecoveryPageContextService
{
    private static void ValidateRead(RecoveryPageContext recoveryPageContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: recoveryPageContext);
        ArgumentNullException.ThrowIfNull(argument: recoveryPageContext.ProviderSource);
        ArgumentNullException.ThrowIfNull(argument: recoveryPageContext.PageSize);
        ArgumentNullException.ThrowIfNull(argument: recoveryPageContext.Read);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateWrite(RecoveryPageContext recoveryPageContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: recoveryPageContext);
        ArgumentNullException.ThrowIfNull(argument: recoveryPageContext.ProviderSource);
        ArgumentNullException.ThrowIfNull(argument: recoveryPageContext.Write);
        ArgumentNullException.ThrowIfNull(argument: recoveryPageContext.Image);
        cancellationToken.ThrowIfCancellationRequested();
    }
}