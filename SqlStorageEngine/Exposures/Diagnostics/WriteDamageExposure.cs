// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Services.Foundations.PageHeaders;
using sql_storage_engine.Services.Foundations.WalRecords;
using sql_storage_engine.Services.Orchestrations.WriteDamage;
using sql_storage_engine.Services.Processings.PageRecoveries;
using sql_storage_engine.Services.Processings.WalReadResults;

namespace sql_storage_engine.Exposures.Diagnostics;
public static class WriteDamageExposure
{
    private static readonly IWriteDamageOrchestrationService service = new WriteDamageOrchestrationService(pageRecovery: new PageHeaderRecoveryProcessingService(pageChecksums: new PageHeaderService()), walRecords: new WalRecordReadProcessingService(walRecords: new WalRecordService()));
    public static byte[] RecoverPage(ReadOnlySpan<byte> page, ReadOnlySpan<byte> verifiedFullPageImage) =>
        service.RecoverPage(page: page, verifiedFullPageImage: verifiedFullPageImage);

    public static WalDamagePolicy ClassifyWal(ReadOnlySpan<byte> bytes) =>
        service.ClassifyWal(bytes: bytes);
}