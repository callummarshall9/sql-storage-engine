// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Services.Foundations.RandomSequences;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Processings.PartialWrites;
internal sealed partial class RandomSequencePartialWriteProcessingService(IRandomSequenceService randomSequences) : IRandomSequencePartialWriteProcessingService
{
    public byte[] Tear(ReadOnlySpan<byte> original, ReadOnlySpan<byte> replacement, PartialWritePattern pattern, int seed, int sectorSize) =>
        TryCatch(operation: context =>
    {
        ValidatePartialWrite(original: context.original, replacement: context.replacement, pattern: context.pattern, seed: context.seed, sectorSize: context.sectorSize);
        return TearCore(partialWriteRequest: context);
    }, context: new PartialWriteRequest { original = original, replacement = replacement, pattern = pattern, seed = seed, sectorSize = sectorSize });

    private byte[] TearCore(PartialWriteRequest partialWriteRequest)
    {
        var original = partialWriteRequest.original;
        var replacement = partialWriteRequest.replacement;
        var pattern = partialWriteRequest.pattern;
        var seed = partialWriteRequest.seed;
        var sectorSize = partialWriteRequest.sectorSize;
        var result = original.ToArray();

        switch (pattern)
        {
            case PartialWritePattern.Prefix:
                replacement[..(replacement.Length / 2)].CopyTo(destination: result);
                break;
            case PartialWritePattern.Suffix:
                replacement[(replacement.Length / 2)..].CopyTo(destination: result.AsSpan(start: replacement.Length / 2));
                break;
            case PartialWritePattern.Sector:
                replacement.Slice(start: (replacement.Length / sectorSize / 2) * sectorSize, length: sectorSize)
            .CopyTo(destination: result.AsSpan(start: (replacement.Length / sectorSize / 2) * sectorSize));
                break;
            case PartialWritePattern.Random:
                var offsets = randomSequences.GenerateRandomSequence(randomSequence: new RandomSequence { Seed = seed, Maximum = replacement.Length, Count = replacement.Length / 4 })
            .Values;
                foreach (var offset in offsets)
                {
                    result[offset] = replacement[offset];
                }

                break;
        }

        return result;
    }
}