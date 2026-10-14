// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Services.Foundations.PageHeaders;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.PageRecoveries;
internal sealed partial class PageHeaderRecoveryProcessingService(IPageHeaderService pageChecksums) : IPageHeaderRecoveryProcessingService
{
    public byte[] RecoverPage(ReadOnlySpan<byte> page, ReadOnlySpan<byte> verifiedFullPageImage) =>
        TryCatch(operation: context =>
    {
        ValidatePageRecovery(page: context.page, verifiedFullPageImage: context.verifiedFullPageImage);
        return RecoverCore(pageRecoveryRequest: context);
    }, context: new PageRecoveryRequest { page = page, verifiedFullPageImage = verifiedFullPageImage });

    private byte[] RecoverCore(PageRecoveryRequest pageRecoveryRequest)
    {
        StorageCorruptionException primaryCorruption;

        try
        {
            pageChecksums.ValidateChecksum(page: pageRecoveryRequest.page, pageSize: pageRecoveryRequest.page.Length);
            return pageRecoveryRequest.page.ToArray();
        }
        catch (StorageCorruptionException exception)
        {
            primaryCorruption = exception;
        }

        if (pageRecoveryRequest.verifiedFullPageImage.IsEmpty)
        {
            throw new UnrecoverablePageCorruptionException(message: "Torn page has no verified full-page recovery image.", innerException: primaryCorruption);
        }

        try
        {
            pageChecksums.ValidateChecksum(page: pageRecoveryRequest.verifiedFullPageImage, pageSize: pageRecoveryRequest.verifiedFullPageImage.Length);
            return pageRecoveryRequest.verifiedFullPageImage.ToArray();
        }
        catch (StorageCorruptionException exception)
        {
            throw new UnrecoverablePageCorruptionException(message: "Torn page has no verified full-page recovery image: " + exception.Message, innerException: new AggregateException(message: "Both page images failed checksum validation.", innerExceptions: [primaryCorruption, exception]));
        }
    }
}