// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Logging;
internal ref struct ReadableWalFormatContext
{
    public ReadOnlySpan<byte> source;
}