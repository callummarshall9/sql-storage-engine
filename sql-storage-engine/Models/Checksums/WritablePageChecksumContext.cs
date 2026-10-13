// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Checksums;
/// <summary>Stack-only transport of page-checksum operation arguments.</summary>
internal ref struct WritablePageChecksumContext
{
    public Span<byte> page;
    public int pageSize;
}