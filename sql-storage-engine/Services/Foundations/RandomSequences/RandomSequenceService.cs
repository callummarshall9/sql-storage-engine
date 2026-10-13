// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Brokers.Randoms;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Foundations.RandomSequences;
internal sealed partial class RandomSequenceService(IRandomSequenceBroker randoms) : IRandomSequenceService
{
    public RandomSequence GenerateRandomSequence(RandomSequence randomSequence) =>
        TryCatch(operation: () =>
    {
        ValidateRandomSequence(randomSequence: randomSequence);

        return new RandomSequence
        {
            Seed = randomSequence.Seed,
            Maximum = randomSequence.Maximum,
            Count = randomSequence.Count,
            Values = Array.AsReadOnly(array: randoms.GenerateOffsets(seed: randomSequence.Seed, maximum: randomSequence.Maximum, count: randomSequence.Count))
        };
    });
}