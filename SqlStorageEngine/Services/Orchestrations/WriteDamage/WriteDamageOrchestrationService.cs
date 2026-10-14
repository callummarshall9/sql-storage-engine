// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Services.Processings.PageRecoveries;
using sql_storage_engine.Services.Processings.WalReadResults;

namespace sql_storage_engine.Services.Orchestrations.WriteDamage;
internal sealed partial class WriteDamageOrchestrationService(IPageHeaderRecoveryProcessingService pageRecovery, IWalRecordReadProcessingService walRecords) : IWriteDamageOrchestrationService
{
    public byte[] RecoverPage(ReadOnlySpan<byte> page, ReadOnlySpan<byte> verifiedFullPageImage) =>
        TryCatch(operation: context =>
    {
        ValidatePageSources(page: context.page, verifiedFullPageImage: context.verifiedFullPageImage);
        return pageRecovery.RecoverPage(page: context.page, verifiedFullPageImage: context.verifiedFullPageImage);
    }, context: new ReadableWriteDamageContext { page = page, verifiedFullPageImage = verifiedFullPageImage });

    public WalDamagePolicy ClassifyWal(ReadOnlySpan<byte> bytes) =>
        TryCatch(operation: context => ValidateWalDamagePolicy(bytes: context.bytes), context: new ReadableWriteDamageContext { bytes = bytes });
}