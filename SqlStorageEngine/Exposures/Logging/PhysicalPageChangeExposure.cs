// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Services.Processings.PhysicalPageChanges;

namespace sql_storage_engine.Exposures.Logging;
public static class PhysicalPageChangeExposure
{
    private static readonly IPhysicalPageChangeCodecProcessingService service = new PhysicalPageChangeCodecProcessingService();
    public static byte[] WritePhysicalPageChange(PhysicalPageChange physicalPageChange) =>
        service.WritePhysicalPageChange(physicalPageChange: physicalPageChange);

    public static PhysicalPageChange ReadPhysicalPageChange(ReadOnlySpan<byte> source) =>
        service.ReadPhysicalPageChange(source: source);
}