// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Pages;

namespace sql_storage_engine.Services.Processings.DatabaseHeaders;
internal interface ISqlCollationDatabaseHeaderProcessingService
{
    void WriteDatabaseHeaderMetadata(Span<byte> page, DatabaseHeader databaseHeader);

    DatabaseHeader ReadDatabaseHeaderMetadata(ReadOnlySpan<byte> page);
}