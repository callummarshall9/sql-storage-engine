// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.Services.Processings.PhysicalPageChanges;
internal interface IPhysicalPageChangeCodecProcessingService
{
    byte[] WritePhysicalPageChange(PhysicalPageChange physicalPageChange);

    PhysicalPageChange ReadPhysicalPageChange(ReadOnlySpan<byte> source);
}