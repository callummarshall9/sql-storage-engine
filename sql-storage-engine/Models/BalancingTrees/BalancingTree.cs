// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.BalancingTrees;
public sealed class BalancingTree<TKey, TValue>
{
    private BalancingTreeNode<TKey, TValue>? root;
    public BalancingTreeNode<TKey, TValue> Root { get => root!; internal set => root = value; }
    public int Order { get; internal set; }
}