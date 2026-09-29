namespace sql_storage_engine;

public sealed class BalancingTreeInternalNode<TKey, TValue> : BalancingTreeNode<TKey, TValue>
{
    public override IReadOnlyList<TKey> Keys => MutableSeparators;
    public override IReadOnlyList<BalancingTreeNode<TKey, TValue>> Children => MutableChildren;

    // Separator i is the smallest key reachable through child i + 1.
    internal List<TKey> MutableSeparators { get; set; } = [];
    internal List<BalancingTreeNode<TKey, TValue>> MutableChildren { get; set; } = [];
}
