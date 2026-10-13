// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Concurrent;

namespace sql_storage_engine.Models.Rows;
internal sealed class SqlCollationRegistry
{
    public ConcurrentDictionary<string, SqlCollation> Registered { get => registered!; init => registered = value; }

    private ConcurrentDictionary<string, SqlCollation>? registered;
}