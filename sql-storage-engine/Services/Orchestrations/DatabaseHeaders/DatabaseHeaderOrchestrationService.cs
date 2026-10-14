// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Services.Processings.DatabaseHeaders;
using sql_storage_engine.Services.Processings.DatabasePages;

namespace sql_storage_engine.Services.Orchestrations.DatabaseHeaders;
internal sealed partial class DatabaseHeaderOrchestrationService : IDatabaseHeaderOrchestrationService
{
    private static readonly IDatabaseHeaderMetadataProcessingService metadata = new DatabaseHeaderMetadataProcessingService();
    private static readonly IPageHeaderDatabasePageProcessingService pages = new PageHeaderDatabasePageProcessingService();
    public void WriteDatabaseHeader(Span<byte> page, DatabaseHeader databaseHeader) =>
        TryCatch(operation: static context =>
    {
        ValidateDatabaseHeaderOnWrite(page: context.page, databaseHeader: context.databaseHeader);
        metadata.WriteDatabaseHeaderMetadata(page: context.page, databaseHeader: context.databaseHeader);
        pages.WriteDatabasePageHeader(page: context.page, pageSize: context.databaseHeader.PageSize);
    }, context: new WritableDatabaseHeaderContext { page = page, databaseHeader = databaseHeader });

    public DatabaseHeader ReadDatabaseHeader(ReadOnlySpan<byte> page) =>
        TryCatch(operation: static context =>
    {
        ValidateDatabaseHeaderSource(page: context.page);
        var databaseHeader = metadata.ReadDatabaseHeaderMetadata(page: context.page);
        pages.CheckDatabasePageHeader(page: context.page, pageSize: databaseHeader.PageSize);
        return databaseHeader;
    }, context: new ReadableDatabaseHeaderContext { page = page });
}