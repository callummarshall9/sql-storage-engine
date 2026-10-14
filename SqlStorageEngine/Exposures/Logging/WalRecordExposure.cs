// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Services.Foundations.WalRecords;

namespace sql_storage_engine.Exposures.Logging;
public static class WalRecordExposure
{
    private static readonly IWalRecordService service = new WalRecordService();
    public static byte[] WriteWalRecord(WalRecord walRecord) =>
        service.WriteWalRecord(walRecord: walRecord);

    public static WalRecord ReadWalRecord(ReadOnlySpan<byte> source) =>
        service.ReadWalRecord(source: source);
}