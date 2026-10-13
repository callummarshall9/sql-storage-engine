// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Dependencies.Randoms;

namespace sql_storage_engine.Brokers.Randoms;
internal sealed class RandomSequenceBroker : IRandomSequenceBroker
{
    public int[] GenerateOffsets(int seed, int maximum, int count) =>
        new SeededRandomDependency(seed: seed).GenerateOffsets(maximum: maximum, count: count);
}