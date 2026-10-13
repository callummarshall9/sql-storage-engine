// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Services.Foundations.RecoveryPages;

namespace sql_storage_engine.Services.Processings.IntegrityPageReads;
internal sealed partial class RecoveryPageContextIntegrityContextProcessingService(RecoveryPageContextService pages) : IRecoveryPageContextIntegrityContextProcessingService
{
    internal static RecoveryPageContextIntegrityContextProcessingService CreateRecoveryPageContextIntegrityContextProcessingService() =>
        new RecoveryPageContextIntegrityContextProcessingService(pages: new RecoveryPageContextService());

    public ValueTask<IntegrityContext> ReadIntegrityContextAsync(IntegrityContext integrityContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateIntegrityContext(integrityContext: integrityContext, cancellationToken: cancellationToken);

        var page = await pages.ReadRecoveryPageContextAsync(recoveryPageContext: integrityContext.Source!with { PageId = integrityContext.PageId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return integrityContext with
        {
            Page = page.Image
        };
    });
}