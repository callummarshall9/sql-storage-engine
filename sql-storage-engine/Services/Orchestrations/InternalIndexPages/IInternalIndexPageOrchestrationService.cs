// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.Services.Orchestrations.InternalIndexPages;
internal interface IInternalIndexPageOrchestrationService
{
    bool CheckInternalIndexPageCapacity(int pageSize, InternalIndexPage internalIndexPage);

    void WriteInternalIndexPage(Span<byte> page, InternalIndexPage internalIndexPage);

    InternalIndexPage ReadInternalIndexPage(ReadOnlySpan<byte> page, ulong expectedPageId);
}