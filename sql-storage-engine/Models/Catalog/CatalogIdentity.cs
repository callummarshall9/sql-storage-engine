// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Numerics;

namespace sql_storage_engine.Models.Catalog;
public sealed record CatalogIdentity
{
    public BigInteger Seed { get; init; }
    public BigInteger Increment { get; init; }
    public bool? IsNotForReplication { get; init; }
}