// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Indexes;
using System.Collections.Generic;
using sql_storage_engine.Services.Orchestrations.InternalIndexPages;

namespace sql_storage_engine.Exposures.Indexes;
public static class InternalIndexPageExposure
{
    private static readonly IInternalIndexPageOrchestrationService service = new InternalIndexPageOrchestrationService();
    public static bool CheckInternalIndexPageCapacity(int pageSize, IEnumerable<IndexKey> separators) =>
        service.CheckInternalIndexPageCapacity(pageSize: pageSize, internalIndexPage: new InternalIndexPage { CapacitySeparators = separators });

    public static void WriteInternalIndexPage(Span<byte> page, InternalIndexPage internalIndexPage) =>
        service.WriteInternalIndexPage(page: page, internalIndexPage: internalIndexPage);

    public static InternalIndexPage ReadInternalIndexPage(ReadOnlySpan<byte> page, ulong expectedPageId) =>
        service.ReadInternalIndexPage(page: page, expectedPageId: expectedPageId);
}