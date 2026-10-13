// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Spatial;
internal readonly ref struct ReadableSqlSpatialBinaryContext
{
    public ReadOnlySpan<byte> value { get; init; }
}