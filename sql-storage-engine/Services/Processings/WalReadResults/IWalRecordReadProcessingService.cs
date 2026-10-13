// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.Services.Processings.WalReadResults;
internal interface IWalRecordReadProcessingService
{
    WalReadResult ReadWalReadResult(ReadOnlySpan<byte> source);

    byte[] WriteWalReadResult(WalReadResult walReadResult);
}