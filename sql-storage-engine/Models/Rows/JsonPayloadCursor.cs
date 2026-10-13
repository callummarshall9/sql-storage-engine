// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Rows;
internal ref struct JsonPayloadCursor
{
    public ReadOnlySpan<byte> Source;
    public int Offset;
}