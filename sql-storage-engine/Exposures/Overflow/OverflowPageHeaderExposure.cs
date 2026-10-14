// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Overflow;
using sql_storage_engine.Services.Processings.OverflowPageHeaders;

namespace sql_storage_engine.Exposures.Overflow;
public static class OverflowPageHeaderExposure
{
    private static readonly IPageHeaderOverflowPageHeaderProcessingService service = new PageHeaderOverflowPageHeaderProcessingService();
    public static int CalculateOverflowPageHeaderCapacity(int pageSize) =>
        service.CalculateOverflowPageHeaderCapacity(pageSize: pageSize);

    public static void WriteOverflowPageHeader(Span<byte> page, ulong pageId, ulong? nextPageId, ReadOnlySpan<byte> payload) =>
        service.WriteOverflowPageHeader(page: page, pageId: pageId, nextPageId: nextPageId, payload: payload);

    public static OverflowPageHeader ReadOverflowPageHeader(ReadOnlySpan<byte> page, ulong expectedPageId) =>
        service.ReadOverflowPageHeader(page: page, expectedPageId: expectedPageId);

    public static ReadOnlyMemory<byte> ReadOverflowPageHeaderPayload(ReadOnlySpan<byte> page, ulong expectedPageId) =>
        service.ReadOverflowPageHeaderPayload(page: page, expectedPageId: expectedPageId);

}