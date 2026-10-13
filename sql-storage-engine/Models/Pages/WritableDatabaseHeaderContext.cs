// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Pages;
internal ref struct WritableDatabaseHeaderContext
{
    public Span<byte> page;
    public DatabaseHeader databaseHeader;
}