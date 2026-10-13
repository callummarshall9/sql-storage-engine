// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Dependencies.Masking;
internal sealed class SqlMaskRandomDependency : Random
{
    public override long NextInt64(long minimum, long maximum) =>
        base.NextInt64(minValue: minimum, maxValue: maximum);

    public override int Next(int minimum, int maximum) =>
        base.Next(minValue: minimum, maxValue: maximum);
}