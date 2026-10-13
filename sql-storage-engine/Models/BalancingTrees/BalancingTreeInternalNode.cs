// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;

namespace sql_storage_engine.Models.BalancingTrees;
public sealed class BalancingTreeInternalNode<TKey, TValue> : BalancingTreeNode<TKey, TValue>
{
    private List<TKey>? mutableSeparators;
    private List<BalancingTreeNode<TKey, TValue>>? mutableChildren;
    internal List<TKey> MutableSeparators { get => mutableSeparators!; set => mutableSeparators = value; }
    internal List<BalancingTreeNode<TKey, TValue>> MutableChildren { get => mutableChildren!; set => mutableChildren = value; }
}