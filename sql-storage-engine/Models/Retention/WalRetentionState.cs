// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;

namespace sql_storage_engine.Models.Retention;
internal sealed class WalRetentionState
{
    public object? Sync;
    public Dictionary<ulong, ulong>? Registrations;
    public ulong NextId;
}