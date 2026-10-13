// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Diagnostics;
internal ref struct ReadableWriteDamageContext
{
    public ReadOnlySpan<byte> page;
    public ReadOnlySpan<byte> verifiedFullPageImage;
    public ReadOnlySpan<byte> bytes;
}