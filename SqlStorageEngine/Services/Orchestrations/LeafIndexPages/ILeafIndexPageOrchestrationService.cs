// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Pages;
using System;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.Services.Orchestrations.LeafIndexPages;
internal interface ILeafIndexPageOrchestrationService
{
    bool CheckLeafIndexPageCapacity(int pageSize, LeafIndexPage leafIndexPage);

    void WriteLeafIndexPage(Span<byte> page, LeafIndexPage leafIndexPage);

    LeafIndexPage ReadLeafIndexPage(ReadOnlySpan<byte> page, ulong expectedPageId);

    PageType ReadLeafIndexPageType(ReadOnlySpan<byte> page);
}