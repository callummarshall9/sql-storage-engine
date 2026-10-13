// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Rows;
internal ref struct ReadableSqlCollationContext
{
    public ReadOnlySpan<byte> value;
    public SqlCollation sqlCollation;
}