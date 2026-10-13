// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Checksums;
using sql_storage_engine.Pages;
using sql_storage_engine.Services.Foundations.PageHeaders;

namespace sql_storage_engine.Services.Processings.DatabasePages;
internal sealed partial class PageHeaderDatabasePageProcessingService : IPageHeaderDatabasePageProcessingService
{
    private static readonly IPageHeaderService pageHeaders = new PageHeaderService();
    public void WriteDatabasePageHeader(Span<byte> page, int pageSize) =>
        TryCatch(operation: static context =>
    {
        ValidateDatabasePageHeader(page: context.page, pageSize: context.pageSize);
        pageHeaders.WritePageHeader(destination: context.page, pageHeader: new PageHeader { PageId = default, PageType = PageType.DatabaseHeader, FormatVersion = new PageFormatVersion { Value = PageHeaderFormat.CurrentVersion }, PageLogSequenceNumber = default, ChecksumAlgorithm = PageChecksumAlgorithm.Crc32 });
        pageHeaders.WriteChecksum(page: context.page, pageSize: context.pageSize);
    }, context: new WritablePageChecksumContext { page = page, pageSize = pageSize });

    public void CheckDatabasePageHeader(ReadOnlySpan<byte> page, int pageSize) =>
        TryCatch(operation: static context =>
    {
        ValidateDatabasePageHeader(page: context.page, pageSize: context.pageSize);
        pageHeaders.ValidateChecksum(page: context.page, pageSize: context.pageSize);
        pageHeaders.CheckPageHeader(pageHeader: pageHeaders.ReadPageHeader(source: context.page), expectedPageId: 0, expectedType: PageType.DatabaseHeader);
    }, context: new ReadablePageChecksumContext { page = page, pageSize = pageSize });
}