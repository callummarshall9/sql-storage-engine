// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.Services.Processings.RecoveryPages;
internal interface IRecoveryPageContextProcessingService
{
    ValueTask<RecoveryPageContext> ReadRecoveryPageContextAsync(RecoveryPageContext recoveryPageContext, CancellationToken cancellationToken);

    ValueTask WriteRecoveryPageContextAsync(RecoveryPageContext recoveryPageContext, CancellationToken cancellationToken);
}