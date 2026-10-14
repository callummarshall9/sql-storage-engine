// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Services.Processings.DatabasePages;
internal interface IPageHeaderDatabasePageProcessingService
{
    void WriteDatabasePageHeader(Span<byte> page, int pageSize);

    void CheckDatabasePageHeader(ReadOnlySpan<byte> page, int pageSize);
}