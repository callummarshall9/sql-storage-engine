// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Catalog;
internal readonly ref struct ReadableCatalogAssemblyContext
{
    public CatalogAssembly catalogAssembly { get; init; }
    public ReadOnlySpan<byte> image { get; init; }
}