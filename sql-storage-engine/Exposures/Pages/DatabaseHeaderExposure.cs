// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Services.Orchestrations.DatabaseHeaders;

namespace sql_storage_engine.Exposures.Pages;
public static class DatabaseHeaderExposure
{
    private static readonly IDatabaseHeaderOrchestrationService service = new DatabaseHeaderOrchestrationService();
    public static void WriteDatabaseHeader(Span<byte> page, DatabaseHeader databaseHeader) =>
        service.WriteDatabaseHeader(page: page, databaseHeader: databaseHeader);

    public static DatabaseHeader ReadDatabaseHeader(ReadOnlySpan<byte> page) =>
        service.ReadDatabaseHeader(page: page);

}