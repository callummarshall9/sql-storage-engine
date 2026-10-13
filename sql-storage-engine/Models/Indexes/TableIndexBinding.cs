// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Models.Indexes;
public sealed record TableIndexBinding
{
    internal object? Service { get; init; }
    internal IndexTreeContext? Context { get; init; }
    public CatalogIndex? Definition { get; init; }
}