// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Brokers.Randoms;
internal interface IRandomSequenceBroker
{
    int[] GenerateOffsets(int seed, int maximum, int count);
}