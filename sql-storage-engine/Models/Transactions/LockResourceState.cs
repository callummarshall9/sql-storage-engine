// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Transactions;
internal sealed class LockResourceState
{
    public Dictionary<TransactionId, LockMode> Granted { get => _granted!; init => _granted = value; }
    public Queue<LockRequest> Waiting { get => _waiting!; init => _waiting = value; }

    private Dictionary<TransactionId, LockMode>? _granted;
    private Queue<LockRequest>? _waiting;
}