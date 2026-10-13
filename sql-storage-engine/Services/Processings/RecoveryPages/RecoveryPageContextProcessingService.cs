// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Services.Foundations.RecoveryPages;

namespace sql_storage_engine.Services.Processings.RecoveryPages;
internal sealed partial class RecoveryPageContextProcessingService(RecoveryPageContextService pages) : IRecoveryPageContextProcessingService
{
    internal static RecoveryPageContextProcessingService CreateRecoveryPageContextProcessingService() =>
        new RecoveryPageContextProcessingService(pages: new RecoveryPageContextService());

    public ValueTask<RecoveryPageContext> ReadRecoveryPageContextAsync(RecoveryPageContext recoveryPageContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRecoveryPageContext(recoveryPageContext: recoveryPageContext, cancellationToken: cancellationToken);

        return await pages.ReadRecoveryPageContextAsync(recoveryPageContext: recoveryPageContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask WriteRecoveryPageContextAsync(RecoveryPageContext recoveryPageContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRecoveryPageContext(recoveryPageContext: recoveryPageContext, cancellationToken: cancellationToken);

        await pages.WriteRecoveryPageContextAsync(recoveryPageContext: recoveryPageContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });
}