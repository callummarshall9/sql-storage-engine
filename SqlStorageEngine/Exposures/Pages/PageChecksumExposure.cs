// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Services.Foundations.PageHeaders;

namespace sql_storage_engine.Exposures.Pages;
/// <summary>Exposes the page-checksum foundation using the original span-based contract.</summary>
public static class PageChecksumExposure
{
    private static readonly IPageHeaderService service = new PageHeaderService();
    public static void WriteChecksum(Span<byte> page, int pageSize) =>
        service.WriteChecksum(page: page, pageSize: pageSize);

    public static void ValidateChecksum(ReadOnlySpan<byte> page, int pageSize) =>
        service.ValidateChecksum(page: page, pageSize: pageSize);
}