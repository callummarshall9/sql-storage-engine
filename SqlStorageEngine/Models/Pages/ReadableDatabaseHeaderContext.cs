// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Pages;
internal ref struct ReadableDatabaseHeaderContext
{
    public ReadOnlySpan<byte> page;
}