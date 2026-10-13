// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Rows;
internal ref struct WritableSqlCollationTextContext
{
    public Span<byte> destination;
    public string value;
}