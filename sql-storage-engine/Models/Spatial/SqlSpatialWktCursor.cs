// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Spatial;
internal ref struct SqlSpatialWktCursor
{
    public ReadOnlySpan<char> Source;
    public int Position;
    public int Depth;
}