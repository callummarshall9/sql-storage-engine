// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Diagnostics;

namespace sql_storage_engine.Services.Processings.PartialWrites;
internal interface IRandomSequencePartialWriteProcessingService
{
    byte[] Tear(ReadOnlySpan<byte> original, ReadOnlySpan<byte> replacement, PartialWritePattern pattern, int seed, int sectorSize);
}