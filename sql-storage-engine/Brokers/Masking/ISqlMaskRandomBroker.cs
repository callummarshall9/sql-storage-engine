// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Brokers.Masking;
internal interface ISqlMaskRandomBroker
{
    long NextInt64(long minimum, long maximum);

    int Next(int minimum, int maximum);
}