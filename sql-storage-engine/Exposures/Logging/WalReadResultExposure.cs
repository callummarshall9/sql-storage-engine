// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Services.Foundations.WalRecords;
using sql_storage_engine.Services.Processings.WalReadResults;

namespace sql_storage_engine.Exposures.Logging;
public static class WalReadResultExposure
{
    private static readonly IWalRecordReadProcessingService service = new WalRecordReadProcessingService(walRecords: new WalRecordService());
    public static WalReadResult ReadWalReadResult(ReadOnlySpan<byte> source) =>
        service.ReadWalReadResult(source: source);

}