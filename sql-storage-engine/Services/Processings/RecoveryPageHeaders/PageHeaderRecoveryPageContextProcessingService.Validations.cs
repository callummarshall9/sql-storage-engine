// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.Services.Processings.RecoveryPageHeaders;
internal sealed partial class PageHeaderRecoveryPageContextProcessingService
{
    private static void ValidateRecoveryPageContext(RecoveryPageContext recoveryPageContext)
    {
        ArgumentNullException.ThrowIfNull(argument: recoveryPageContext);
        ArgumentNullException.ThrowIfNull(argument: recoveryPageContext.Image);
        ArgumentNullException.ThrowIfNull(argument: recoveryPageContext.Change);
    }
}