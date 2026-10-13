// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Pages;
using sql_storage_engine.Services.Foundations.PageHeaders;

namespace sql_storage_engine.Exposures.Pages;
public static class PageHeaderExposure
{
    private static readonly IPageHeaderService service = new PageHeaderService();
    public static void WritePageHeader(Span<byte> destination, PageHeader pageHeader) =>
        service.WritePageHeader(destination: destination, pageHeader: pageHeader);

    public static PageHeader ReadPageHeader(ReadOnlySpan<byte> source) =>
        service.ReadPageHeader(source: source);

    public static void CheckPageHeader(PageHeader pageHeader, ulong expectedPageId, PageType? expectedType = null) =>
        service.CheckPageHeader(pageHeader: pageHeader, expectedPageId: expectedPageId, expectedType: expectedType);

    public static void DecodeFuzzInput(System.ReadOnlyMemory<byte> source) =>
        service.ReadPageHeader(source: source.Span);
}