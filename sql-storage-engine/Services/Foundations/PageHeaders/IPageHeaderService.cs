// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Pages;

namespace sql_storage_engine.Services.Foundations.PageHeaders;
internal interface IPageHeaderService
{
    void WritePageHeader(Span<byte> destination, PageHeader pageHeader);

    PageHeader ReadPageHeader(ReadOnlySpan<byte> source);

    void CheckPageHeader(PageHeader pageHeader, ulong expectedPageId, PageType? expectedType = null);

    void WriteChecksum(Span<byte> page, int pageSize);

    void ValidateChecksum(ReadOnlySpan<byte> page, int pageSize);
}