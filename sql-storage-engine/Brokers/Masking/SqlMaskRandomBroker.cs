// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Dependencies.Masking;

namespace sql_storage_engine.Brokers.Masking;
internal sealed class SqlMaskRandomBroker : ISqlMaskRandomBroker
{
    private readonly SqlMaskRandomDependency dependency = new SqlMaskRandomDependency();
    public long NextInt64(long minimum, long maximum) =>
        dependency.NextInt64(minimum: minimum, maximum: maximum);

    public int Next(int minimum, int maximum) =>
        dependency.Next(minimum: minimum, maximum: maximum);
}