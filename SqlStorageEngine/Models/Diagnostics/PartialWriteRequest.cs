// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Diagnostics;

namespace sql_storage_engine.Models.Diagnostics;
internal ref struct PartialWriteRequest
{
    public ReadOnlySpan<byte> original;
    public ReadOnlySpan<byte> replacement;
    public PartialWritePattern pattern;
    public int seed;
    public int sectorSize;
}