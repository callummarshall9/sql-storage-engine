// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Indexes;
using System.Collections.Generic;
using sql_storage_engine.Services.Orchestrations.LeafIndexPages;

namespace sql_storage_engine.Exposures.Indexes;
public static class LeafIndexPageExposure
{
    private static readonly ILeafIndexPageOrchestrationService service = new LeafIndexPageOrchestrationService();
    public static bool CheckLeafIndexPageCapacity(int pageSize, IEnumerable<LeafIndexEntry> entries) =>
        service.CheckLeafIndexPageCapacity(pageSize: pageSize, leafIndexPage: new LeafIndexPage { CapacityEntries = entries });

    public static void WriteLeafIndexPage(Span<byte> page, LeafIndexPage leafIndexPage) =>
        service.WriteLeafIndexPage(page: page, leafIndexPage: leafIndexPage);

    public static LeafIndexPage ReadLeafIndexPage(ReadOnlySpan<byte> page, ulong expectedPageId) =>
        service.ReadLeafIndexPage(page: page, expectedPageId: expectedPageId);

    public static void DecodeFuzzInput(System.ReadOnlyMemory<byte> source) =>
        service.ReadLeafIndexPage(page: source.Span, expectedPageId: 1);
}