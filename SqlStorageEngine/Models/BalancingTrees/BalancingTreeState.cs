// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.BalancingTrees;
internal sealed class BalancingTreeState<TKey, TValue>
{
    public BalancingTree<TKey, TValue>? Tree { get; set; }
    public int Count { get; set; }
}