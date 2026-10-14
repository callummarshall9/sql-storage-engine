// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Pages;
internal ref struct WritablePageHeaderContext
{
    public Span<byte> destination;
    public PageHeader pageHeader;
}