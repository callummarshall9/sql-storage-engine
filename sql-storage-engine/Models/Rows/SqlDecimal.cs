// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Numerics;

namespace sql_storage_engine.Models.Rows;
public readonly record struct SqlDecimal
{
    public BigInteger Coefficient { get; init; }
    public byte Scale { get; init; }
}