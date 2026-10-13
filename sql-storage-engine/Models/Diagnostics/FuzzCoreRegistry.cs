// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace sql_storage_engine.Models.Diagnostics;
internal sealed record FuzzCoreRegistry
{
    public Action<ReadOnlyMemory<byte>>[]? Decoders { get; init; }
    public IReadOnlyList<FuzzTarget>? Targets { get; init; }
}