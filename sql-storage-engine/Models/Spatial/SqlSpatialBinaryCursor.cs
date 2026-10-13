// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Spatial;
internal ref struct SqlSpatialBinaryCursor
{
    public ReadOnlySpan<byte> Source;
    public int Offset;
    public int Nodes;
}