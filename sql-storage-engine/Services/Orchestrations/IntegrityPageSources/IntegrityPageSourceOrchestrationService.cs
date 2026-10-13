// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Pages;
using sql_storage_engine.Services.Processings.IntegrityPageReads;
using sql_storage_engine.Services.Processings.IntegrityPageHeaders;
using sql_storage_engine.Services.Processings.OverflowPageHeaders;

namespace sql_storage_engine.Services.Orchestrations.IntegrityPageSources;
internal sealed partial class IntegrityPageSourceOrchestrationService(RecoveryPageContextIntegrityContextProcessingService pages, IntegrityPageHeaderProcessingService headers, PageHeaderOverflowPageHeaderProcessingService overflow) : IIntegrityPageSourceOrchestrationService
{
    internal static IntegrityPageSourceOrchestrationService CreateIntegrityPageSourceOrchestrationService() =>
        new IntegrityPageSourceOrchestrationService(pages: RecoveryPageContextIntegrityContextProcessingService.CreateRecoveryPageContextIntegrityContextProcessingService(), headers: IntegrityPageHeaderProcessingService.CreateIntegrityPageHeaderProcessingService(), overflow: new PageHeaderOverflowPageHeaderProcessingService());

    public ValueTask<IntegrityContext> ReadIntegrityContextAsync(IntegrityContext integrityContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateIntegrityContext(integrityContext: integrityContext, cancellationToken: cancellationToken);

        var result = await pages.ReadIntegrityContextAsync(integrityContext: integrityContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        result = headers.CheckIntegrityContext(integrityContext: result);

        if (result.PageHeader!.PageType == PageType.Overflow)
        {
            overflow.ReadOverflowPageHeader(page: result.Page!, expectedPageId: result.PageId);
        }

        return result;
    });

    public ValueTask<IntegrityContext> ReadRawIntegrityContextAsync(IntegrityContext integrityContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateIntegrityContext(integrityContext: integrityContext, cancellationToken: cancellationToken);

        return await pages.ReadIntegrityContextAsync(integrityContext: integrityContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });
}