// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;

namespace sql_storage_engine.Models.BalancingTrees;
public abstract class BalancingTreeNode<TKey, TValue>
{
    public BalancingTreeInternalNode<TKey, TValue>? Parent { get; internal set; }

    private IReadOnlyList<TKey>? keys;
    public IReadOnlyList<TKey> Keys { get => keys!; internal set => keys = value; }

    private IReadOnlyList<BalancingTreeNode<TKey, TValue>>? children;
    public IReadOnlyList<BalancingTreeNode<TKey, TValue>> Children { get => children!; internal set => children = value; }
    public bool IsLeaf { get; internal set; }
}