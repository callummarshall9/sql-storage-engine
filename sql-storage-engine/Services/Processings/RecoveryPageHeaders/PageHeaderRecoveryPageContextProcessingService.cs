// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Services.Foundations.PageHeaders;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.RecoveryPageHeaders;
internal sealed partial class PageHeaderRecoveryPageContextProcessingService(PageHeaderService headers) : IPageHeaderRecoveryPageContextProcessingService
{
    internal static PageHeaderRecoveryPageContextProcessingService CreatePageHeaderRecoveryPageContextProcessingService() =>
        new PageHeaderRecoveryPageContextProcessingService(headers: new PageHeaderService());

    public RecoveryPageContext CheckRecoveryPageContext(RecoveryPageContext recoveryPageContext) =>
        TryCatch(operation: () =>
    {
        ValidateRecoveryPageContext(recoveryPageContext: recoveryPageContext);
        headers.ValidateChecksum(page: recoveryPageContext.Image!, pageSize: recoveryPageContext.Image!.Length);
        var header = headers.ReadPageHeader(source: recoveryPageContext.Image);
        headers.CheckPageHeader(pageHeader: header, expectedPageId: recoveryPageContext.PageId, expectedType: recoveryPageContext.Change!.PageType);

        if (recoveryPageContext.CheckExpectedLsn && header.PageLogSequenceNumber.Value != recoveryPageContext.ExpectedLsn)
        {
            throw new StorageCorruptionException(message: "Logged after-image LSN does not match its WAL record.");
        }

        return recoveryPageContext with
        {
            StoredLsn = header.PageLogSequenceNumber.Value
        };
    });
}