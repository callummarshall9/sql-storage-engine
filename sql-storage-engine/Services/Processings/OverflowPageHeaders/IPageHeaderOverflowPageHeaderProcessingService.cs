// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Overflow;

namespace sql_storage_engine.Services.Processings.OverflowPageHeaders;
internal interface IPageHeaderOverflowPageHeaderProcessingService
{
    int CalculateOverflowPageHeaderCapacity(int pageSize);

    void WriteOverflowPageHeader(Span<byte> page, ulong pageId, ulong? nextPageId, ReadOnlySpan<byte> payload);

    OverflowPageHeader ReadOverflowPageHeader(ReadOnlySpan<byte> page, ulong expectedPageId);

    ReadOnlyMemory<byte> ReadOverflowPageHeaderPayload(ReadOnlySpan<byte> page, ulong expectedPageId);
}