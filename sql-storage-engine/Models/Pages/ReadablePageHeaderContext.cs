// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Pages;
internal ref struct ReadablePageHeaderContext
{
    public ReadOnlySpan<byte> source;
    public PageHeader header;
}