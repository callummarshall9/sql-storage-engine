// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Checksums;
/// <summary>Stack-only transport of page-checksum operation arguments.</summary>
internal ref struct ReadablePageChecksumContext
{
    public ReadOnlySpan<byte> page;
    public int pageSize;
}