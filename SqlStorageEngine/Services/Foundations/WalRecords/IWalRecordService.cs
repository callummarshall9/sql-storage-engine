// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.Services.Foundations.WalRecords;
internal interface IWalRecordService
{
    byte[] WriteWalRecord(WalRecord walRecord);

    WalRecord ReadWalRecord(ReadOnlySpan<byte> source);
}