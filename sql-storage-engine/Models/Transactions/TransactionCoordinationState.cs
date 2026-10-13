// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;

namespace sql_storage_engine.Models.Transactions;
internal sealed class TransactionCoordinationState
{
    public object? Sync;
    public Dictionary<ulong, bool>? LeaseKinds;
    public ulong NextId;
    public int Pending;
    public int Readers;
    public bool Closed;
}