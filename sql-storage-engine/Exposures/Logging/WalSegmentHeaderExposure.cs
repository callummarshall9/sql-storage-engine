// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Services.Foundations.WalSegmentHeaders;

namespace sql_storage_engine.Exposures.Logging;
public static class WalSegmentHeaderExposure
{
    private static readonly IWalSegmentHeaderService service = new WalSegmentHeaderService();
    public static byte[] WriteWalSegmentHeader(WalSegmentHeader walSegmentHeader) =>
        service.WriteWalSegmentHeader(walSegmentHeader: walSegmentHeader);

    public static WalSegmentHeader ReadWalSegmentHeader(ReadOnlySpan<byte> source) =>
        service.ReadWalSegmentHeader(source: source);
}