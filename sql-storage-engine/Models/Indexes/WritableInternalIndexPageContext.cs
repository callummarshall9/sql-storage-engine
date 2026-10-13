// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Indexes;
internal ref struct WritableInternalIndexPageContext
{
    public Span<byte> page;
    public InternalIndexPage internalIndexPage;
}