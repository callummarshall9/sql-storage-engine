// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Rows;
internal readonly ref struct ReadableJsonPayloadContext
{
    public ReadOnlySpan<byte> source { get; init; }
}