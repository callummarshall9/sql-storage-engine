// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Models.Catalog;
internal sealed class CatalogPageResourceConfiguration
{
    public Func<int>? PageSize { get; set; }
    public Func<ushort, CancellationToken, ValueTask<ulong>>? Allocate { get; set; }
    public Func<ulong, CancellationToken, ValueTask>? Free { get; set; }
    public Func<ulong, Memory<byte>, CancellationToken, ValueTask>? Read { get; set; }
    public Func<ulong, ReadOnlyMemory<byte>, CancellationToken, ValueTask>? Write { get; set; }
}