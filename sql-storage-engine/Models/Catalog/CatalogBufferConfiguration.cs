// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Models.Catalog;
internal sealed class CatalogBufferConfiguration
{
    public Func<CancellationToken, ValueTask>? Flush { get; set; }
    public Func<ulong, CancellationToken, ValueTask>? Discard { get; set; }
}