// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Rows;
internal readonly ref struct ReadableXmlPayloadContext
{
    public ReadOnlySpan<byte> source { get; init; }
}