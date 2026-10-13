// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Pages;
using System;

namespace sql_storage_engine.Services.Orchestrations.DatabaseHeaders;
internal interface IDatabaseHeaderOrchestrationService
{
    void WriteDatabaseHeader(Span<byte> page, DatabaseHeader databaseHeader);

    DatabaseHeader ReadDatabaseHeader(ReadOnlySpan<byte> page);
}