// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;

namespace sql_storage_engine.Models.Indexes;
internal sealed class IndexAllocationContext
{
    public bool Tracking { get; set; }
    public ICollection<ulong>? Pages { get; set; }
}