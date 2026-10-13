// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Diagnostics;
internal ref struct PageRecoveryRequest
{
    public ReadOnlySpan<byte> page;
    public ReadOnlySpan<byte> verifiedFullPageImage;
}