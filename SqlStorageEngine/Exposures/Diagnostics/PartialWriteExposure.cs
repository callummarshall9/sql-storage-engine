// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Brokers.Randoms;
using sql_storage_engine.Services.Foundations.RandomSequences;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Services.Processings.PartialWrites;

namespace sql_storage_engine.Exposures.Diagnostics;
public static class PartialWriteExposure
{
    private static readonly IRandomSequencePartialWriteProcessingService service = new RandomSequencePartialWriteProcessingService(randomSequences: new RandomSequenceService(randoms: new RandomSequenceBroker()));
    public static byte[] Tear(ReadOnlySpan<byte> original, ReadOnlySpan<byte> replacement, PartialWritePattern pattern, int seed = 1729, int sectorSize = 512) =>
        service.Tear(original: original, replacement: replacement, pattern: pattern, seed: seed, sectorSize: sectorSize);
}