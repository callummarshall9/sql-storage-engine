// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.Services.Foundations.WalSegmentHeaders;
internal interface IWalSegmentHeaderService
{
    byte[] WriteWalSegmentHeader(WalSegmentHeader walSegmentHeader);

    WalSegmentHeader ReadWalSegmentHeader(ReadOnlySpan<byte> source);
}