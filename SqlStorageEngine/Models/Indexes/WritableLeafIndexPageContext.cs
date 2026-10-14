// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Indexes;
internal ref struct WritableLeafIndexPageContext
{
    public Span<byte> page;
    public LeafIndexPage leafIndexPage;
}