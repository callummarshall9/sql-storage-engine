// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Services.Foundations.PageHeaders;

namespace sql_storage_engine.Services.Processings.IntegrityPageHeaders;
internal sealed partial class IntegrityPageHeaderProcessingService(PageHeaderService headers) : IIntegrityPageHeaderProcessingService
{
    internal static IntegrityPageHeaderProcessingService CreateIntegrityPageHeaderProcessingService() =>
        new IntegrityPageHeaderProcessingService(headers: new PageHeaderService());

    public IntegrityContext CheckIntegrityContext(IntegrityContext integrityContext) =>
        TryCatch(operation: () =>
    {
        ValidateIntegrityContext(integrityContext: integrityContext, cancellationToken: default);
        headers.ValidateChecksum(page: integrityContext.Page!, pageSize: integrityContext.Page!.Length);
        var header = headers.ReadPageHeader(source: integrityContext.Page);
        headers.CheckPageHeader(pageHeader: header, expectedPageId: integrityContext.PageId);

        return integrityContext with
        {
            PageHeader = header
        };
    });
}