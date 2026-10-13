// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;

namespace sql_storage_engine.Models.BalancingTrees;
public sealed class BalancingTreeConfiguration<TKey, TValue>
{
    public int Order { get; set; }
    public IComparer<TKey>? KeyComparer { get; set; }
    public IEqualityComparer<TValue>? ValueComparer { get; set; }
}